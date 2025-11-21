using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Custom;
using WebApi.Models;
using WebApi.Models.DTOs;
using WebApi.Services;

namespace WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsuariosController : ControllerBase
    {
        private readonly DbMasterContext _dbMasterContext;
        private readonly Utilidades _utilidades;
        private readonly IS3Service _s3Service;
        private readonly ILogger<UsuariosController> _logger;

        public UsuariosController(
            DbMasterContext dbMasterContext,
            Utilidades utilidades,
            IS3Service s3Service,
            ILogger<UsuariosController> logger)
        {
            _dbMasterContext = dbMasterContext;
            _utilidades = utilidades;
            _s3Service = s3Service;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene el perfil de un usuario por ID
        /// </summary>
        [HttpGet]
        [Route("Obtener/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> ObtenerUsuario(int id)
        {
            try
            {
                var usuario = await _dbMasterContext.Usuarios
                    .Where(u => u.IdUsuario == id && u.Activo == true)
                    .Select(u => new
                    {
                        u.IdUsuario,
                        u.Nombre,
                        u.Username,
                        u.Correo,
                        u.Edad,
                        u.Rol,
                        u.FotoPerfil,
                        u.Bio,
                        u.FechaCreacion,
                        TotalPublicaciones = u.Publicaciones.Count(p => p.Estado == "Publicado"),
                        TotalSeguidores = u.SeguidoreIdSeguidoNavigations.Count,
                        TotalSiguiendo = u.SeguidoreIdUsuarioNavigations.Count
                    })
                    .FirstOrDefaultAsync();

                if (usuario == null)
                {
                    return NotFound(new { isSuccess = false, mensaje = "El usuario no existe o está inactivo" });
                }

                return Ok(new { isSuccess = true, data = usuario });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuario {Id}", id);
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al obtener el usuario"
                });
            }
        }

        /// <summary>
        /// Obtiene el perfil del usuario autenticado
        /// </summary>
        [HttpGet]
        [Route("MiPerfil")]
        [Authorize]
        public async Task<IActionResult> ObtenerMiPerfil()
        {
            try
            {
                var userIdClaim = User.FindFirst("idUsuario")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { isSuccess = false, mensaje = "No se pudo validar el usuario" });
                }

                var usuario = await _dbMasterContext.Usuarios
                    .Where(u => u.IdUsuario == userId)
                    .Select(u => new
                    {
                        u.IdUsuario,
                        u.Nombre,
                        u.Username,
                        u.Correo,
                        u.Edad,
                        u.Rol,
                        u.FotoPerfil,
                        u.Bio,
                        u.Activo,
                        u.FechaCreacion,
                        u.FechaActualizacion,
                        TotalPublicaciones = u.Publicaciones.Count,
                        TotalSeguidores = u.SeguidoreIdSeguidoNavigations.Count,
                        TotalSiguiendo = u.SeguidoreIdUsuarioNavigations.Count
                    })
                    .FirstOrDefaultAsync();

                if (usuario == null)
                {
                    return NotFound(new { isSuccess = false, mensaje = "El usuario no existe" });
                }

                return Ok(new { isSuccess = true, data = usuario });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener perfil del usuario autenticado");
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al obtener el perfil"
                });
            }
        }

        /// <summary>
        /// Actualiza el perfil del usuario autenticado
        /// </summary>
        [HttpPut]
        [Route("ActualizarPerfil")]
        [Authorize]
        [RequestSizeLimit(10 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
        public async Task<IActionResult> ActualizarPerfil(
            [FromForm] string? nombre,
            [FromForm] string? username,
            [FromForm] int? edad,
            [FromForm] string? bio,
            [FromForm] IFormFile? fotoPerfil)
        {
            try
            {
                var userIdClaim = User.FindFirst("idUsuario")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { isSuccess = false, mensaje = "No se pudo validar el usuario" });
                }

                var usuario = await _dbMasterContext.Usuarios.FindAsync(userId);
                if (usuario == null)
                {
                    return NotFound(new { isSuccess = false, mensaje = "El usuario no existe" });
                }

                // Actualizar nombre si se proporciona
                if (!string.IsNullOrWhiteSpace(nombre))
                {
                    if (nombre.Length > 100)
                    {
                        return BadRequest(new { isSuccess = false, mensaje = "El nombre no puede exceder 100 caracteres" });
                    }
                    usuario.Nombre = nombre.Trim();
                }

                // Actualizar username si se proporciona
                if (!string.IsNullOrWhiteSpace(username))
                {
                    if (username.Length > 50)
                    {
                        return BadRequest(new { isSuccess = false, mensaje = "El username no puede exceder 50 caracteres" });
                    }

                    // Verificar si el username ya existe (excepto el usuario actual)
                    var usernameExiste = await _dbMasterContext.Usuarios
                        .AnyAsync(u => u.Username.ToLower() == username.Trim().ToLower() && u.IdUsuario != userId);

                    if (usernameExiste)
                    {
                        return BadRequest(new { isSuccess = false, mensaje = "El username ya está en uso" });
                    }

                    usuario.Username = username.Trim();
                }

                // Actualizar edad si se proporciona
                if (edad.HasValue)
                {
                    if (edad.Value < 0)
                    {
                        return BadRequest(new { isSuccess = false, mensaje = "La edad no puede ser negativa" });
                    }
                    usuario.Edad = edad.Value;
                }

                // Actualizar bio si se proporciona
                if (bio != null)
                {
                    if (bio.Length > 500)
                    {
                        return BadRequest(new { isSuccess = false, mensaje = "La bio no puede exceder 500 caracteres" });
                    }
                    usuario.Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();
                }

                // Manejar foto de perfil si se proporciona
                if (fotoPerfil != null && fotoPerfil.Length > 0)
                {
                    // Validar que es una imagen válida
                    if (!_s3Service.IsValidImageFile(fotoPerfil))
                    {
                        return BadRequest(new
                        {
                            isSuccess = false,
                            mensaje = "El archivo no es una imagen válida o excede el tamaño máximo permitido (10MB). Formatos permitidos: JPG, PNG, GIF, WEBP"
                        });
                    }

                    try
                    {
                        // Eliminar foto anterior si existe
                        if (!string.IsNullOrEmpty(usuario.FotoPerfil))
                        {
                            await _s3Service.DeleteFileAsync(usuario.FotoPerfil);
                            _logger.LogInformation("Foto de perfil anterior eliminada: {FotoPerfil}", usuario.FotoPerfil);
                        }

                        // Subir nueva foto
                        using var stream = fotoPerfil.OpenReadStream();
                        usuario.FotoPerfil = await _s3Service.UploadFileAsync(stream, fotoPerfil.FileName, fotoPerfil.ContentType);
                        _logger.LogInformation("Nueva foto de perfil subida: {FotoPerfil}", usuario.FotoPerfil);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al actualizar foto de perfil en S3");
                        return StatusCode(500, new
                        {
                            isSuccess = false,
                            mensaje = "Error al actualizar la foto de perfil. Por favor, intenta nuevamente."
                        });
                    }
                }

                usuario.FechaActualizacion = DateTime.UtcNow;

                _dbMasterContext.Usuarios.Update(usuario);
                await _dbMasterContext.SaveChangesAsync();

                _logger.LogInformation("Perfil actualizado exitosamente. ID: {IdUsuario}", userId);

                return Ok(new
                {
                    isSuccess = true,
                    mensaje = "Perfil actualizado correctamente",
                    data = new
                    {
                        idUsuario = usuario.IdUsuario,
                        nombre = usuario.Nombre,
                        username = usuario.Username,
                        edad = usuario.Edad,
                        bio = usuario.Bio,
                        fotoPerfil = usuario.FotoPerfil
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar perfil");
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al actualizar el perfil"
                });
            }
        }

        /// <summary>
        /// Cambia la contraseña del usuario autenticado
        /// </summary>
        [HttpPut]
        [Route("CambiarPassword")]
        [Authorize]
        public async Task<IActionResult> CambiarPassword([FromBody] CambiarPasswordRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.PasswordActual) || string.IsNullOrWhiteSpace(request.PasswordNuevo))
                {
                    return BadRequest(new { isSuccess = false, mensaje = "La contraseña actual y la nueva contraseña son requeridas" });
                }

                if (request.PasswordNuevo.Length < 6)
                {
                    return BadRequest(new { isSuccess = false, mensaje = "La nueva contraseña debe tener al menos 6 caracteres" });
                }

                var userIdClaim = User.FindFirst("idUsuario")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { isSuccess = false, mensaje = "No se pudo validar el usuario" });
                }

                var usuario = await _dbMasterContext.Usuarios.FindAsync(userId);
                if (usuario == null)
                {
                    return NotFound(new { isSuccess = false, mensaje = "El usuario no existe" });
                }

                // Verificar contraseña actual
                var passwordActualHash = _utilidades.EncriptarSHA256(request.PasswordActual);
                if (usuario.PasswordHash != passwordActualHash)
                {
                    return BadRequest(new { isSuccess = false, mensaje = "La contraseña actual es incorrecta" });
                }

                // Actualizar contraseña
                usuario.PasswordHash = _utilidades.EncriptarSHA256(request.PasswordNuevo);
                usuario.FechaActualizacion = DateTime.UtcNow;

                _dbMasterContext.Usuarios.Update(usuario);
                await _dbMasterContext.SaveChangesAsync();

                _logger.LogInformation("Contraseña cambiada exitosamente. ID: {IdUsuario}", userId);

                return Ok(new { isSuccess = true, mensaje = "Contraseña cambiada correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar contraseña");
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al cambiar la contraseña"
                });
            }
        }

        /// <summary>
        /// Lista todas las publicaciones de un usuario
        /// </summary>
        [HttpGet]
        [Route("{id}/Publicaciones")]
        [AllowAnonymous]
        public async Task<IActionResult> ObtenerPublicacionesUsuario(int id)
        {
            try
            {
                var publicaciones = await _dbMasterContext.Publicaciones
                    .Where(p => p.IdUsuario == id && p.Estado == "Publicado")
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
                        Categoria = p.IdCategoriaNavigation != null ? new
                        {
                            p.IdCategoriaNavigation.IdCategoria,
                            p.IdCategoriaNavigation.Nombre
                        } : null,
                        TotalComentarios = p.Comentarios.Count,
                        TotalReacciones = p.Reacciones.Count
                    })
                    .ToListAsync();

                return Ok(new { isSuccess = true, data = publicaciones, total = publicaciones.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener publicaciones del usuario {Id}", id);
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al obtener las publicaciones"
                });
            }
        }

        /// <summary>
        /// Busca usuarios por nombre o username
        /// </summary>
        [HttpGet]
        [Route("Buscar")]
        [AllowAnonymous]
        public async Task<IActionResult> BuscarUsuarios([FromQuery] string? termino)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(termino))
                {
                    return BadRequest(new { isSuccess = false, mensaje = "El término de búsqueda es requerido" });
                }

                var usuarios = await _dbMasterContext.Usuarios
                    .Where(u => u.Activo == true &&
                                (u.Nombre.Contains(termino) || u.Username.Contains(termino)))
                    .Select(u => new
                    {
                        u.IdUsuario,
                        u.Nombre,
                        u.Username,
                        u.FotoPerfil,
                        u.Bio,
                        TotalPublicaciones = u.Publicaciones.Count(p => p.Estado == "Publicado"),
                        TotalSeguidores = u.SeguidoreIdSeguidoNavigations.Count
                    })
                    .Take(20)
                    .ToListAsync();

                return Ok(new { isSuccess = true, data = usuarios, total = usuarios.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar usuarios");
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al buscar usuarios"
                });
            }
        }
    }

    // DTOs para las peticiones
    public class CambiarPasswordRequest
    {
        public required string PasswordActual { get; set; }
        public required string PasswordNuevo { get; set; }
    }
}

