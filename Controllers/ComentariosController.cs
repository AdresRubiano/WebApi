using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Models;

namespace WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ComentariosController : ControllerBase
    {
        private readonly DbMasterContext _dbMasterContext;
        private readonly ILogger<ComentariosController> _logger;

        public ComentariosController(
            DbMasterContext dbMasterContext,
            ILogger<ComentariosController> logger)
        {
            _dbMasterContext = dbMasterContext;
            _logger = logger;
        }

        /// <summary>
        /// Lista todos los comentarios de una publicación
        /// </summary>
        [HttpGet]
        [Route("ListarPorPublicacion/{idPublicacion}")]
        [AllowAnonymous]
        public async Task<IActionResult> ListarComentariosPorPublicacion(int idPublicacion)
        {
            try
            {
                var comentarios = await _dbMasterContext.Comentarios
                    .Where(c => c.IdPublicacion == idPublicacion)
                    .Include(c => c.IdUsuarioNavigation)
                    .OrderByDescending(c => c.FechaComentario)
                    .Select(c => new
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
                    })
                    .ToListAsync();

                return Ok(new { isSuccess = true, data = comentarios, total = comentarios.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar comentarios de la publicación {IdPublicacion}", idPublicacion);
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al listar los comentarios"
                });
            }
        }

        /// <summary>
        /// Obtiene un comentario por ID
        /// </summary>
        [HttpGet]
        [Route("Obtener/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> ObtenerComentario(int id)
        {
            try
            {
                var comentario = await _dbMasterContext.Comentarios
                    .Where(c => c.IdComentario == id)
                    .Include(c => c.IdUsuarioNavigation)
                    .Include(c => c.IdPublicacionNavigation)
                    .Select(c => new
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
                        Publicacion = new
                        {
                            c.IdPublicacionNavigation.IdPublicacion,
                            c.IdPublicacionNavigation.Titulo
                        },
                        TotalReacciones = c.Reacciones.Count
                    })
                    .FirstOrDefaultAsync();

                if (comentario == null)
                {
                    return NotFound(new { isSuccess = false, mensaje = "El comentario no existe" });
                }

                return Ok(new { isSuccess = true, data = comentario });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener comentario {Id}", id);
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al obtener el comentario"
                });
            }
        }

        /// <summary>
        /// Crea un nuevo comentario
        /// </summary>
        [HttpPost]
        [Route("Crear")]
        public async Task<IActionResult> CrearComentario([FromBody] CrearComentarioRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Comentario))
                {
                    return BadRequest(new { isSuccess = false, mensaje = "El comentario es obligatorio" });
                }

                var userIdClaim = User.FindFirst("idUsuario")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { isSuccess = false, mensaje = "No se pudo validar el usuario" });
                }

                // Verificar que el usuario existe
                var usuarioExiste = await _dbMasterContext.Usuarios.FindAsync(userId);
                if (usuarioExiste == null)
                {
                    return BadRequest(new { isSuccess = false, mensaje = "El usuario no existe" });
                }

                // Verificar que la publicación existe
                var publicacionExiste = await _dbMasterContext.Publicaciones.FindAsync(request.IdPublicacion);
                if (publicacionExiste == null)
                {
                    return BadRequest(new { isSuccess = false, mensaje = "La publicación no existe" });
                }

                var nuevoComentario = new Comentario
                {
                    Comentario1 = request.Comentario.Trim(),
                    FechaComentario = DateTime.UtcNow,
                    Editado = false,
                    IdUsuario = userId,
                    IdPublicacion = request.IdPublicacion
                };

                await _dbMasterContext.Comentarios.AddAsync(nuevoComentario);
                await _dbMasterContext.SaveChangesAsync();

                _logger.LogInformation("Comentario creado exitosamente. ID: {IdComentario}", nuevoComentario.IdComentario);

                return Ok(new
                {
                    isSuccess = true,
                    mensaje = "Comentario creado exitosamente",
                    data = new
                    {
                        idComentario = nuevoComentario.IdComentario,
                        comentario = nuevoComentario.Comentario1,
                        fechaComentario = nuevoComentario.FechaComentario
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear comentario");
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al crear el comentario"
                });
            }
        }

        /// <summary>
        /// Actualiza un comentario existente
        /// </summary>
        [HttpPut]
        [Route("Actualizar/{id}")]
        public async Task<IActionResult> ActualizarComentario(int id, [FromBody] ActualizarComentarioRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Comentario))
                {
                    return BadRequest(new { isSuccess = false, mensaje = "El comentario es obligatorio" });
                }

                var userIdClaim = User.FindFirst("idUsuario")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { isSuccess = false, mensaje = "No se pudo validar el usuario" });
                }

                var comentario = await _dbMasterContext.Comentarios.FindAsync(id);
                if (comentario == null)
                {
                    return NotFound(new { isSuccess = false, mensaje = "El comentario no existe" });
                }

                // Verificar que el usuario es el dueño del comentario
                if (comentario.IdUsuario != userId)
                {
                    return Forbid();
                }

                comentario.Comentario1 = request.Comentario.Trim();
                comentario.Editado = true;

                _dbMasterContext.Comentarios.Update(comentario);
                await _dbMasterContext.SaveChangesAsync();

                _logger.LogInformation("Comentario actualizado exitosamente. ID: {IdComentario}", id);

                return Ok(new
                {
                    isSuccess = true,
                    mensaje = "Comentario actualizado correctamente",
                    data = new
                    {
                        idComentario = comentario.IdComentario,
                        comentario = comentario.Comentario1,
                        editado = comentario.Editado
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar comentario {Id}", id);
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al actualizar el comentario"
                });
            }
        }

        /// <summary>
        /// Elimina un comentario
        /// </summary>
        [HttpDelete]
        [Route("Eliminar/{id}")]
        public async Task<IActionResult> EliminarComentario(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst("idUsuario")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { isSuccess = false, mensaje = "No se pudo validar el usuario" });
                }

                var comentario = await _dbMasterContext.Comentarios
                    .Include(c => c.Reacciones)
                    .FirstOrDefaultAsync(c => c.IdComentario == id);

                if (comentario == null)
                {
                    return NotFound(new { isSuccess = false, mensaje = "El comentario no existe" });
                }

                // Verificar que el usuario es el dueño del comentario o es admin
                var usuario = await _dbMasterContext.Usuarios.FindAsync(userId);
                if (comentario.IdUsuario != userId && usuario?.Rol != "Admin")
                {
                    return Forbid();
                }

                // Eliminar reacciones asociadas
                if (comentario.Reacciones.Any())
                {
                    _dbMasterContext.Reacciones.RemoveRange(comentario.Reacciones);
                }

                _dbMasterContext.Comentarios.Remove(comentario);
                await _dbMasterContext.SaveChangesAsync();

                _logger.LogInformation("Comentario eliminado exitosamente. ID: {IdComentario}", id);

                return Ok(new { isSuccess = true, mensaje = "Comentario eliminado correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar comentario {Id}", id);
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al eliminar el comentario"
                });
            }
        }
    }

    // DTOs para las peticiones
    public class CrearComentarioRequest
    {
        public required string Comentario { get; set; }
        public required int IdPublicacion { get; set; }
    }

    public class ActualizarComentarioRequest
    {
        public required string Comentario { get; set; }
    }
}

