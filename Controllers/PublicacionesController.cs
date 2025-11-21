using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Models;
using WebApi.Models.DTOs;
using WebApi.Services;

namespace WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PublicacionesController : ControllerBase
    {
        private readonly DbMasterContext _dbMasterContext;
        private readonly IS3Service _s3Service;
        private readonly ILogger<PublicacionesController> _logger;

        public PublicacionesController(
            DbMasterContext dbMasterContext,
            IS3Service s3Service,
            ILogger<PublicacionesController> logger)
        {
            _dbMasterContext = dbMasterContext;
            _s3Service = s3Service;
            _logger = logger;
        }

        /// <summary>
        /// Crea una nueva publicación con o sin imagen
        /// </summary>
        /// <param name="titulo">Título de la publicación</param>
        /// <param name="contenido">Contenido de la publicación</param>
        /// <param name="etiquetas">Etiquetas separadas por comas (opcional)</param>
        /// <param name="idCategoria">ID de la categoría (opcional)</param>
        /// <param name="imagen">Archivo de imagen (opcional)</param>
        [HttpPost]
        [Route("Crear")]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10MB máximo
        [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
        public async Task<IActionResult> CrearPublicacion(
            [FromForm] string titulo,
            [FromForm] string contenido,
            [FromForm] string? etiquetas,
            [FromForm] int? idCategoria,
            [FromForm] IFormFile? imagen)
        {
            try
            {
               
                if (string.IsNullOrWhiteSpace(titulo))
                {
                    return BadRequest(new { isSuccess = false, mensaje = "El título es obligatorio" });
                }

                if (string.IsNullOrWhiteSpace(contenido))
                {
                    return BadRequest(new { isSuccess = false, mensaje = "El contenido es obligatorio" });
                }

               
                if (titulo.Length > 200)
                {
                    return BadRequest(new { isSuccess = false, mensaje = "El título no puede exceder 200 caracteres" });
                }

               
                var userIdClaim = User.FindFirst("idUsuario")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { isSuccess = false, mensaje = "No se pudo validar el usuario" });
                }

              
                var usuarioExiste = await _dbMasterContext.Usuarios.FindAsync(userId);
                if (usuarioExiste == null)
                {
                    return BadRequest(new { isSuccess = false, mensaje = "El usuario no existe" });
                }

                
                if (idCategoria.HasValue)
                {
                    var categoriaExiste = await _dbMasterContext.Categorias.FindAsync(idCategoria.Value);
                    if (categoriaExiste == null)
                    {
                        return BadRequest(new { isSuccess = false, mensaje = "La categoría no existe" });
                    }
                }

                // Validar y subir imagen si se proporciona
                string? imageUrl = null;
                
                // Logging para diagnóstico
                _logger.LogInformation("Imagen recibida - IsNull: {IsNull}, Length: {Length}, FileName: {FileName}, ContentType: {ContentType}",
                    imagen == null, imagen?.Length ?? 0, imagen?.FileName ?? "N/A", imagen?.ContentType ?? "N/A");

                if (imagen != null && imagen.Length > 0)
                {
                    _logger.LogInformation("Procesando imagen: {FileName}, Tamaño: {Length} bytes, ContentType: {ContentType}",
                        imagen.FileName, imagen.Length, imagen.ContentType);

                    // Validar que es una imagen válida
                    if (!_s3Service.IsValidImageFile(imagen))
                    {
                        _logger.LogWarning("Imagen no válida: {FileName}, ContentType: {ContentType}, Length: {Length}",
                            imagen.FileName, imagen.ContentType, imagen.Length);
                        return BadRequest(new
                        {
                            isSuccess = false,
                            mensaje = "El archivo no es una imagen válida o excede el tamaño máximo permitido (10MB). Formatos permitidos: JPG, PNG, GIF, WEBP"
                        });
                    }

                    try
                    {
                        using var stream = imagen.OpenReadStream();
                        imageUrl = await _s3Service.UploadFileAsync(stream, imagen.FileName, imagen.ContentType);
                        _logger.LogInformation("Imagen subida exitosamente: {ImageUrl}", imageUrl);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al subir imagen a S3");
                        return StatusCode(500, new
                        {
                            isSuccess = false,
                            mensaje = "Error al subir la imagen. Por favor, intenta nuevamente."
                        });
                    }
                }
                else if (imagen != null)
                {
                    _logger.LogWarning("Imagen recibida pero está vacía (Length: {Length})", imagen.Length);
                }
                else
                {
                    _logger.LogInformation("No se recibió ninguna imagen en la petición");
                }
                var nuevaPublicacion = new Publicacione
                {
                    Titulo = titulo.Trim(),
                    Contenido = contenido.Trim(),
                    Etiquetas = etiquetas?.Trim(),
                    ImagenUrl = imageUrl,
                    Estado = "Publicado",
                    FechaPublicacion = DateTime.UtcNow,
                    FechaActualizacion = DateTime.UtcNow,
                    IdUsuario = userId,
                    IdCategoria = idCategoria
                };

                await _dbMasterContext.Publicaciones.AddAsync(nuevaPublicacion);
                await _dbMasterContext.SaveChangesAsync();

                _logger.LogInformation("Publicación creada exitosamente. ID: {IdPublicacion}", nuevaPublicacion.IdPublicacion);

                return Ok(new
                {
                    isSuccess = true,
                    mensaje = "Publicación creada exitosamente",
                    data = new
                    {
                        idPublicacion = nuevaPublicacion.IdPublicacion,
                        titulo = nuevaPublicacion.Titulo,
                        imagenUrl = imageUrl
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear publicación");
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al crear la publicación"
                });
            }
        }

        /// <summary>
        /// Lista todas las publicaciones con información del usuario y categoría
        /// </summary>
        [HttpGet]
        [Route("Listar")]
        public async Task<IActionResult> ListarPublicaciones()
        {
            try
            {
                var publicaciones = await _dbMasterContext.Publicaciones
                    .Include(p => p.IdUsuarioNavigation)
                    .Include(p => p.IdCategoriaNavigation)
                    .OrderByDescending(p => p.FechaPublicacion)
                    .Select(p => new
                    {
                        p.IdPublicacion,
                        p.Titulo,
                        p.Contenido,
                        p.Etiquetas,
                        p.ImagenUrl,
                        p.Estado,
                        p.FechaPublicacion,
                        p.FechaActualizacion,
                        Usuario = new
                        {
                            p.IdUsuarioNavigation.IdUsuario,
                            p.IdUsuarioNavigation.Nombre,
                            p.IdUsuarioNavigation.Correo
                        },
                        Categoria = p.IdCategoriaNavigation != null ? new
                        {
                            p.IdCategoriaNavigation.IdCategoria,
                            p.IdCategoriaNavigation.Nombre
                        } : null
                    })
                    .ToListAsync();

                return Ok(new { isSuccess = true, data = publicaciones, total = publicaciones.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar publicaciones");
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al listar las publicaciones"
                });
            }
        }

        /// <summary>
        /// Obtiene el feed de publicaciones de los usuarios que sigue el usuario autenticado
        /// </summary>
        [HttpGet]
        [Route("Feed")]
        public async Task<IActionResult> ObtenerFeed()
        {
            try
            {
                var userIdClaim = User.FindFirst("idUsuario")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { isSuccess = false, mensaje = "No se pudo validar el usuario" });
                }

                // Obtener IDs de usuarios que sigue
                var usuariosSeguidos = await _dbMasterContext.Seguidores
                    .Where(s => s.IdUsuario == userId)
                    .Select(s => s.IdSeguido)
                    .ToListAsync();

                if (!usuariosSeguidos.Any())
                {
                    return Ok(new { isSuccess = true, data = new List<object>(), total = 0, mensaje = "No sigues a ningún usuario aún" });
                }

                var publicaciones = await _dbMasterContext.Publicaciones
                    .Where(p => usuariosSeguidos.Contains(p.IdUsuario) && p.Estado == "Publicado")
                    .Include(p => p.IdUsuarioNavigation)
                    .Include(p => p.IdCategoriaNavigation)
                    .OrderByDescending(p => p.FechaPublicacion)
                    .Select(p => new
                    {
                        p.IdPublicacion,
                        p.Titulo,
                        p.Contenido,
                        p.Etiquetas,
                        p.ImagenUrl,
                        p.FechaPublicacion,
                        p.FechaActualizacion,
                        Usuario = new
                        {
                            p.IdUsuarioNavigation.IdUsuario,
                            p.IdUsuarioNavigation.Nombre,
                            p.IdUsuarioNavigation.Username,
                            p.IdUsuarioNavigation.FotoPerfil
                        },
                        Categoria = p.IdCategoriaNavigation != null ? new
                        {
                            p.IdCategoriaNavigation.IdCategoria,
                            p.IdCategoriaNavigation.Nombre
                        } : null,
                        TotalComentarios = p.Comentarios.Count,
                        TotalReacciones = p.Reacciones.Count
                    })
                    .Take(50)
                    .ToListAsync();

                return Ok(new { isSuccess = true, data = publicaciones, total = publicaciones.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener feed de publicaciones");
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al obtener el feed"
                });
            }
        }

        /// <summary>
        /// Lista las publicaciones del usuario autenticado
        /// </summary>
        [HttpGet]
        [Route("MisPublicaciones")]
        public async Task<IActionResult> MisPublicaciones()
        {
            try
            {
                var userIdClaim = User.FindFirst("idUsuario")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { isSuccess = false, mensaje = "No se pudo validar el usuario" });
                }

                var publicaciones = await _dbMasterContext.Publicaciones
                    .Where(p => p.IdUsuario == userId)
                    .Include(p => p.IdCategoriaNavigation)
                    .OrderByDescending(p => p.FechaPublicacion)
                    .Select(p => new
                    {
                        p.IdPublicacion,
                        p.Titulo,
                        p.Contenido,
                        p.Etiquetas,
                        p.ImagenUrl,
                        p.Estado,
                        p.FechaPublicacion,
                        p.FechaActualizacion,
                        Categoria = p.IdCategoriaNavigation != null ? new
                        {
                            p.IdCategoriaNavigation.IdCategoria,
                            p.IdCategoriaNavigation.Nombre
                        } : null
                    })
                    .ToListAsync();

                return Ok(new { isSuccess = true, data = publicaciones, total = publicaciones.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar mis publicaciones");
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al listar tus publicaciones"
                });
            }
        }

        /// <summary>
        /// Obtiene una publicación por ID
        /// </summary>
        [HttpGet]
        [Route("Obtener/{id}")]
        public async Task<IActionResult> ObtenerPublicacion(int id)
        {
            try
            {
                var publicacion = await _dbMasterContext.Publicaciones
                    .Include(p => p.IdUsuarioNavigation)
                    .Include(p => p.IdCategoriaNavigation)
                    .Where(p => p.IdPublicacion == id)
                    .Select(p => new
                    {
                        p.IdPublicacion,
                        p.Titulo,
                        p.Contenido,
                        p.Etiquetas,
                        p.ImagenUrl,
                        p.Estado,
                        p.FechaPublicacion,
                        p.FechaActualizacion,
                        Usuario = new
                        {
                            p.IdUsuarioNavigation.IdUsuario,
                            p.IdUsuarioNavigation.Nombre,
                            p.IdUsuarioNavigation.Correo
                        },
                        Categoria = p.IdCategoriaNavigation != null ? new
                        {
                            p.IdCategoriaNavigation.IdCategoria,
                            p.IdCategoriaNavigation.Nombre
                        } : null
                    })
                    .FirstOrDefaultAsync();

                if (publicacion == null)
                {
                    return NotFound(new { isSuccess = false, mensaje = "La publicación no existe" });
                }

                return Ok(new { isSuccess = true, data = publicacion });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener publicación {Id}", id);
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al obtener la publicación"
                });
            }
        }

        /// <summary>
        /// Actualiza una publicación existente
        /// </summary>
        [HttpPut]
        [Route("Actualizar/{id}")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
        public async Task<IActionResult> ActualizarPublicacion(
            int id,
            [FromForm] string? titulo,
            [FromForm] string? contenido,
            [FromForm] string? etiquetas,
            [FromForm] int? idCategoria,
            [FromForm] IFormFile? imagen)
        {
            try
            {
                var userIdClaim = User.FindFirst("idUsuario")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { isSuccess = false, mensaje = "No se pudo validar el usuario" });
                }

                var publicacion = await _dbMasterContext.Publicaciones.FindAsync(id);
                if (publicacion == null)
                {
                    return NotFound(new { isSuccess = false, mensaje = "La publicación no existe" });
                }

                // Verificar que el usuario es el dueño de la publicación
                if (publicacion.IdUsuario != userId)
                {
                    return Forbid();
                }

                // Actualizar campos si se proporcionan
                if (!string.IsNullOrWhiteSpace(titulo))
                {
                    if (titulo.Length > 200)
                    {
                        return BadRequest(new { isSuccess = false, mensaje = "El título no puede exceder 200 caracteres" });
                    }
                    publicacion.Titulo = titulo.Trim();
                }

                if (!string.IsNullOrWhiteSpace(contenido))
                {
                    publicacion.Contenido = contenido.Trim();
                }

                if (etiquetas != null)
                {
                    publicacion.Etiquetas = string.IsNullOrWhiteSpace(etiquetas) ? null : etiquetas.Trim();
                }

                if (idCategoria.HasValue)
                {
                    var categoriaExiste = await _dbMasterContext.Categorias.FindAsync(idCategoria.Value);
                    if (categoriaExiste == null)
                    {
                        return BadRequest(new { isSuccess = false, mensaje = "La categoría no existe" });
                    }
                    publicacion.IdCategoria = idCategoria;
                }

                // Manejar nueva imagen si se proporciona
                if (imagen != null && imagen.Length > 0)
                {
                    // Validar que es una imagen válida
                    if (!_s3Service.IsValidImageFile(imagen))
                    {
                        return BadRequest(new
                        {
                            isSuccess = false,
                            mensaje = "El archivo no es una imagen válida o excede el tamaño máximo permitido (10MB). Formatos permitidos: JPG, PNG, GIF, WEBP"
                        });
                    }

                    try
                    {
                        // Eliminar imagen anterior si existe
                        if (!string.IsNullOrEmpty(publicacion.ImagenUrl))
                        {
                            await _s3Service.DeleteFileAsync(publicacion.ImagenUrl);
                            _logger.LogInformation("Imagen anterior eliminada: {ImageUrl}", publicacion.ImagenUrl);
                        }

                        // Subir nueva imagen
                        using var stream = imagen.OpenReadStream();
                        publicacion.ImagenUrl = await _s3Service.UploadFileAsync(stream, imagen.FileName, imagen.ContentType);
                        _logger.LogInformation("Nueva imagen subida: {ImageUrl}", publicacion.ImagenUrl);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al actualizar imagen en S3");
                        return StatusCode(500, new
                        {
                            isSuccess = false,
                            mensaje = "Error al actualizar la imagen. Por favor, intenta nuevamente."
                        });
                    }
                }

                publicacion.FechaActualizacion = DateTime.UtcNow;

                _dbMasterContext.Publicaciones.Update(publicacion);
                await _dbMasterContext.SaveChangesAsync();

                _logger.LogInformation("Publicación actualizada exitosamente. ID: {IdPublicacion}", id);

                return Ok(new 
                { 
                    isSuccess = true, 
                    mensaje = "Publicación actualizada correctamente",
                    data = new
                    {
                        idPublicacion = publicacion.IdPublicacion,
                        titulo = publicacion.Titulo,
                        contenido = publicacion.Contenido,
                        etiquetas = publicacion.Etiquetas,
                        imagenUrl = publicacion.ImagenUrl,
                        estado = publicacion.Estado,
                        fechaActualizacion = publicacion.FechaActualizacion
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar publicación {Id}", id);
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al actualizar la publicación"
                });
            }
        }

        /// <summary>
        /// Elimina una publicación y su imagen asociada de S3
        /// </summary>
        [HttpDelete]
        [Route("Eliminar/{id}")]
        public async Task<IActionResult> EliminarPublicacion(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst("idUsuario")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { isSuccess = false, mensaje = "No se pudo validar el usuario" });
                }

                var publicacion = await _dbMasterContext.Publicaciones.FindAsync(id);
                if (publicacion == null)
                {
                    return NotFound(new { isSuccess = false, mensaje = "La publicación no existe" });
                }

                // Verificar que el usuario es el dueño de la publicación
                if (publicacion.IdUsuario != userId)
                {
                    return Forbid();
                }

                // Eliminar imagen de S3 si existe
                if (!string.IsNullOrEmpty(publicacion.ImagenUrl))
                {
                    var imagenEliminada = await _s3Service.DeleteFileAsync(publicacion.ImagenUrl);
                    if (imagenEliminada)
                    {
                        _logger.LogInformation("Imagen eliminada de S3: {ImageUrl}", publicacion.ImagenUrl);
                    }
                    else
                    {
                        _logger.LogWarning("No se pudo eliminar la imagen de S3: {ImageUrl}", publicacion.ImagenUrl);
                    }
                }

                // Eliminar publicación de la base de datos
                _dbMasterContext.Publicaciones.Remove(publicacion);
                await _dbMasterContext.SaveChangesAsync();

                _logger.LogInformation("Publicación eliminada exitosamente. ID: {IdPublicacion}", id);

                return Ok(new { isSuccess = true, mensaje = "Publicación eliminada correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar publicación {Id}", id);
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al eliminar la publicación"
                });
            }
        }

        /// <summary>
        /// Valida si una URL de imagen es válida y existe en S3
        /// </summary>
        /// <param name="request">Objeto con la URL de la imagen a validar</param>
        [HttpPost]
        [Route("ValidarImagen")]
        public async Task<IActionResult> ValidarImagen([FromBody] ValidarImagenRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.ImageUrl))
                {
                    return BadRequest(new 
                    { 
                        isSuccess = false, 
                        isValid = false,
                        mensaje = "La URL de la imagen es requerida" 
                    });
                }

                var imageUrl = request.ImageUrl;

                // Validar formato de URL
                var isValidFormat = _s3Service.IsValidImageUrl(imageUrl);
                
                if (!isValidFormat)
                {
                    return Ok(new 
                    { 
                        isSuccess = true, 
                        isValid = false,
                        mensaje = "La URL de la imagen no es válida o no pertenece al bucket configurado" 
                    });
                }

                // Verificar si existe en S3
                var exists = await _s3Service.ImageExistsAsync(imageUrl);

                if (!exists)
                {
                    return Ok(new 
                    { 
                        isSuccess = true, 
                        isValid = false,
                        mensaje = "La imagen no existe en S3" 
                    });
                }

                return Ok(new 
                { 
                    isSuccess = true, 
                    isValid = true,
                    mensaje = "La imagen es válida y existe" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al validar imagen: {ImageUrl}", request?.ImageUrl);
                return StatusCode(500, new
                {
                    isSuccess = false,
                    isValid = false,
                    mensaje = "Error interno del servidor al validar la imagen"
                });
            }
        }

        /// <summary>
        /// Lista publicaciones por categoría
        /// </summary>
        [HttpGet]
        [Route("PorCategoria/{idCategoria}")]
        [AllowAnonymous]
        public async Task<IActionResult> ListarPublicacionesPorCategoria(int idCategoria)
        {
            try
            {
                var categoriaExiste = await _dbMasterContext.Categorias.FindAsync(idCategoria);
                if (categoriaExiste == null)
                {
                    return NotFound(new { isSuccess = false, mensaje = "La categoría no existe" });
                }

                var publicaciones = await _dbMasterContext.Publicaciones
                    .Where(p => p.IdCategoria == idCategoria && p.Estado == "Publicado")
                    .Include(p => p.IdUsuarioNavigation)
                    .Include(p => p.IdCategoriaNavigation)
                    .OrderByDescending(p => p.FechaPublicacion)
                    .Select(p => new
                    {
                        p.IdPublicacion,
                        p.Titulo,
                        p.Contenido,
                        p.Etiquetas,
                        p.ImagenUrl,
                        p.Estado,
                        p.FechaPublicacion,
                        p.FechaActualizacion,
                        Usuario = new
                        {
                            p.IdUsuarioNavigation.IdUsuario,
                            p.IdUsuarioNavigation.Nombre,
                            p.IdUsuarioNavigation.Username,
                            p.IdUsuarioNavigation.FotoPerfil
                        },
                        Categoria = new
                        {
                            p.IdCategoriaNavigation!.IdCategoria,
                            p.IdCategoriaNavigation.Nombre
                        },
                        TotalComentarios = p.Comentarios.Count,
                        TotalReacciones = p.Reacciones.Count
                    })
                    .ToListAsync();

                return Ok(new { isSuccess = true, data = publicaciones, total = publicaciones.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar publicaciones por categoría {IdCategoria}", idCategoria);
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al listar las publicaciones"
                });
            }
        }

        /// <summary>
        /// Busca publicaciones por término (título, contenido o etiquetas)
        /// </summary>
        [HttpGet]
        [Route("Buscar")]
        [AllowAnonymous]
        public async Task<IActionResult> BuscarPublicaciones([FromQuery] string? termino)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(termino))
                {
                    return BadRequest(new { isSuccess = false, mensaje = "El término de búsqueda es requerido" });
                }

                var publicaciones = await _dbMasterContext.Publicaciones
                    .Where(p => p.Estado == "Publicado" &&
                                (p.Titulo.Contains(termino) || 
                                 p.Contenido.Contains(termino) || 
                                 (p.Etiquetas != null && p.Etiquetas.Contains(termino))))
                    .Include(p => p.IdUsuarioNavigation)
                    .Include(p => p.IdCategoriaNavigation)
                    .OrderByDescending(p => p.FechaPublicacion)
                    .Select(p => new
                    {
                        p.IdPublicacion,
                        p.Titulo,
                        p.Contenido,
                        p.Etiquetas,
                        p.ImagenUrl,
                        p.FechaPublicacion,
                        Usuario = new
                        {
                            p.IdUsuarioNavigation.IdUsuario,
                            p.IdUsuarioNavigation.Nombre,
                            p.IdUsuarioNavigation.Username
                        },
                        Categoria = p.IdCategoriaNavigation != null ? new
                        {
                            p.IdCategoriaNavigation.IdCategoria,
                            p.IdCategoriaNavigation.Nombre
                        } : null,
                        TotalComentarios = p.Comentarios.Count,
                        TotalReacciones = p.Reacciones.Count
                    })
                    .Take(50)
                    .ToListAsync();

                return Ok(new { isSuccess = true, data = publicaciones, total = publicaciones.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar publicaciones");
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al buscar publicaciones"
                });
            }
        }

        /// <summary>
        /// Obtiene las publicaciones más populares (por número de reacciones)
        /// </summary>
        [HttpGet]
        [Route("Populares")]
        [AllowAnonymous]
        public async Task<IActionResult> ObtenerPublicacionesPopulares([FromQuery] int limite = 10)
        {
            try
            {
                if (limite < 1 || limite > 50)
                {
                    limite = 10;
                }

                var publicaciones = await _dbMasterContext.Publicaciones
                    .Where(p => p.Estado == "Publicado")
                    .Include(p => p.IdUsuarioNavigation)
                    .Include(p => p.IdCategoriaNavigation)
                    .OrderByDescending(p => p.Reacciones.Count)
                    .ThenByDescending(p => p.FechaPublicacion)
                    .Select(p => new
                    {
                        p.IdPublicacion,
                        p.Titulo,
                        p.Contenido,
                        p.Etiquetas,
                        p.ImagenUrl,
                        p.FechaPublicacion,
                        Usuario = new
                        {
                            p.IdUsuarioNavigation.IdUsuario,
                            p.IdUsuarioNavigation.Nombre,
                            p.IdUsuarioNavigation.Username,
                            p.IdUsuarioNavigation.FotoPerfil
                        },
                        Categoria = p.IdCategoriaNavigation != null ? new
                        {
                            p.IdCategoriaNavigation.IdCategoria,
                            p.IdCategoriaNavigation.Nombre
                        } : null,
                        TotalComentarios = p.Comentarios.Count,
                        TotalReacciones = p.Reacciones.Count
                    })
                    .Take(limite)
                    .ToListAsync();

                return Ok(new { isSuccess = true, data = publicaciones, total = publicaciones.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener publicaciones populares");
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al obtener las publicaciones populares"
                });
            }
        }

        /// <summary>
        /// Obtiene una publicación con todos sus detalles (comentarios y reacciones)
        /// </summary>
        [HttpGet]
        [Route("Detalle/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> ObtenerPublicacionDetalle(int id)
        {
            try
            {
                var publicacion = await _dbMasterContext.Publicaciones
                    .Where(p => p.IdPublicacion == id && p.Estado == "Publicado")
                    .Include(p => p.IdUsuarioNavigation)
                    .Include(p => p.IdCategoriaNavigation)
                    .Include(p => p.Comentarios)
                        .ThenInclude(c => c.IdUsuarioNavigation)
                    .Include(p => p.Reacciones)
                        .ThenInclude(r => r.IdUsuarioNavigation)
                    .Select(p => new
                    {
                        p.IdPublicacion,
                        p.Titulo,
                        p.Contenido,
                        p.Etiquetas,
                        p.ImagenUrl,
                        p.FechaPublicacion,
                        p.FechaActualizacion,
                        Usuario = new
                        {
                            p.IdUsuarioNavigation.IdUsuario,
                            p.IdUsuarioNavigation.Nombre,
                            p.IdUsuarioNavigation.Username,
                            p.IdUsuarioNavigation.FotoPerfil
                        },
                        Categoria = p.IdCategoriaNavigation != null ? new
                        {
                            p.IdCategoriaNavigation.IdCategoria,
                            p.IdCategoriaNavigation.Nombre
                        } : null,
                        Comentarios = p.Comentarios.OrderByDescending(c => c.FechaComentario).Select(c => new
                        {
                            c.IdComentario,
                            c.Comentario1,
                            c.FechaComentario,
                            c.Editado,
                            Usuario = new
                            {
                                c.IdUsuarioNavigation.IdUsuario,
                                c.IdUsuarioNavigation.Nombre,
                                c.IdUsuarioNavigation.Username,
                                c.IdUsuarioNavigation.FotoPerfil
                            },
                            TotalReacciones = c.Reacciones.Count
                        }).ToList(),
                        Reacciones = p.Reacciones.GroupBy(r => r.Tipo).Select(g => new
                        {
                            Tipo = g.Key,
                            Cantidad = g.Count()
                        }).ToList(),
                        TotalComentarios = p.Comentarios.Count,
                        TotalReacciones = p.Reacciones.Count
                    })
                    .FirstOrDefaultAsync();

                if (publicacion == null)
                {
                    return NotFound(new { isSuccess = false, mensaje = "La publicación no existe o no está publicada" });
                }

                return Ok(new { isSuccess = true, data = publicacion });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalle de publicación {Id}", id);
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al obtener el detalle de la publicación"
                });
            }
        }
    }
}
