using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Models;

namespace WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ReaccionesController : ControllerBase
    {
        private readonly DbMasterContext _dbMasterContext;
        private readonly ILogger<ReaccionesController> _logger;

        public ReaccionesController(
            DbMasterContext dbMasterContext,
            ILogger<ReaccionesController> logger)
        {
            _dbMasterContext = dbMasterContext;
            _logger = logger;
        }

        /// <summary>
        /// Lista todas las reacciones de una publicación
        /// </summary>
        [HttpGet]
        [Route("ListarPorPublicacion/{idPublicacion}")]
        [AllowAnonymous]
        public async Task<IActionResult> ListarReaccionesPorPublicacion(int idPublicacion)
        {
            try
            {
                var reacciones = await _dbMasterContext.Reacciones
                    .Where(r => r.IdPublicacion == idPublicacion)
                    .Include(r => r.IdUsuarioNavigation)
                    .GroupBy(r => r.Tipo)
                    .Select(g => new
                    {
                        Tipo = g.Key,
                        Cantidad = g.Count(),
                        Usuarios = g.Select(r => new
                        {
                            r.IdUsuarioNavigation.IdUsuario,
                            r.IdUsuarioNavigation.Nombre,
                            r.IdUsuarioNavigation.Username
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new { isSuccess = true, data = reacciones });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar reacciones de la publicación {IdPublicacion}", idPublicacion);
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al listar las reacciones"
                });
            }
        }

        /// <summary>
        /// Lista todas las reacciones de un comentario
        /// </summary>
        [HttpGet]
        [Route("ListarPorComentario/{idComentario}")]
        [AllowAnonymous]
        public async Task<IActionResult> ListarReaccionesPorComentario(int idComentario)
        {
            try
            {
                var reacciones = await _dbMasterContext.Reacciones
                    .Where(r => r.IdComentario == idComentario)
                    .Include(r => r.IdUsuarioNavigation)
                    .GroupBy(r => r.Tipo)
                    .Select(g => new
                    {
                        Tipo = g.Key,
                        Cantidad = g.Count(),
                        Usuarios = g.Select(r => new
                        {
                            r.IdUsuarioNavigation.IdUsuario,
                            r.IdUsuarioNavigation.Nombre,
                            r.IdUsuarioNavigation.Username
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new { isSuccess = true, data = reacciones });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar reacciones del comentario {IdComentario}", idComentario);
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al listar las reacciones"
                });
            }
        }

        /// <summary>
        /// Verifica si el usuario autenticado ya reaccionó a una publicación o comentario
        /// </summary>
        [HttpGet]
        [Route("VerificarReaccion")]
        public async Task<IActionResult> VerificarReaccion([FromQuery] int? idPublicacion, [FromQuery] int? idComentario)
        {
            try
            {
                if (!idPublicacion.HasValue && !idComentario.HasValue)
                {
                    return BadRequest(new { isSuccess = false, mensaje = "Debe proporcionar idPublicacion o idComentario" });
                }

                var userIdClaim = User.FindFirst("idUsuario")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { isSuccess = false, mensaje = "No se pudo validar el usuario" });
                }

                var reaccion = await _dbMasterContext.Reacciones
                    .Where(r => r.IdUsuario == userId &&
                                (idPublicacion.HasValue ? r.IdPublicacion == idPublicacion.Value : r.IdComentario == idComentario.Value))
                    .FirstOrDefaultAsync();

                if (reaccion == null)
                {
                    return Ok(new { isSuccess = true, tieneReaccion = false });
                }

                return Ok(new
                {
                    isSuccess = true,
                    tieneReaccion = true,
                    data = new
                    {
                        idReaccion = reaccion.IdReaccion,
                        tipo = reaccion.Tipo,
                        fechaReaccion = reaccion.FechaReaccion
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar reacción");
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al verificar la reacción"
                });
            }
        }

        /// <summary>
        /// Crea o actualiza una reacción a una publicación o comentario
        /// </summary>
        [HttpPost]
        [Route("Reaccionar")]
        public async Task<IActionResult> Reaccionar([FromBody] CrearReaccionRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Tipo))
                {
                    return BadRequest(new { isSuccess = false, mensaje = "El tipo de reacción es obligatorio" });
                }

                if (!request.IdPublicacion.HasValue && !request.IdComentario.HasValue)
                {
                    return BadRequest(new { isSuccess = false, mensaje = "Debe proporcionar idPublicacion o idComentario" });
                }

                var userIdClaim = User.FindFirst("idUsuario")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { isSuccess = false, mensaje = "No se pudo validar el usuario" });
                }

                // Validar que la publicación o comentario existe
                if (request.IdPublicacion.HasValue)
                {
                    var publicacionExiste = await _dbMasterContext.Publicaciones.FindAsync(request.IdPublicacion.Value);
                    if (publicacionExiste == null)
                    {
                        return BadRequest(new { isSuccess = false, mensaje = "La publicación no existe" });
                    }
                }

                if (request.IdComentario.HasValue)
                {
                    var comentarioExiste = await _dbMasterContext.Comentarios.FindAsync(request.IdComentario.Value);
                    if (comentarioExiste == null)
                    {
                        return BadRequest(new { isSuccess = false, mensaje = "El comentario no existe" });
                    }
                }

                // Verificar si ya existe una reacción del usuario
                var reaccionExistente = await _dbMasterContext.Reacciones
                    .Where(r => r.IdUsuario == userId &&
                                (request.IdPublicacion.HasValue ? r.IdPublicacion == request.IdPublicacion.Value : r.IdComentario == request.IdComentario.Value))
                    .FirstOrDefaultAsync();

                if (reaccionExistente != null)
                {
                    // Si es el mismo tipo, eliminar la reacción (toggle)
                    if (reaccionExistente.Tipo == request.Tipo)
                    {
                        _dbMasterContext.Reacciones.Remove(reaccionExistente);
                        await _dbMasterContext.SaveChangesAsync();

                        _logger.LogInformation("Reacción eliminada. ID: {IdReaccion}", reaccionExistente.IdReaccion);

                        return Ok(new
                        {
                            isSuccess = true,
                            mensaje = "Reacción eliminada",
                            accion = "eliminada"
                        });
                    }
                    else
                    {
                        // Actualizar el tipo de reacción
                        reaccionExistente.Tipo = request.Tipo;
                        reaccionExistente.FechaReaccion = DateTime.UtcNow;

                        _dbMasterContext.Reacciones.Update(reaccionExistente);
                        await _dbMasterContext.SaveChangesAsync();

                        _logger.LogInformation("Reacción actualizada. ID: {IdReaccion}", reaccionExistente.IdReaccion);

                        return Ok(new
                        {
                            isSuccess = true,
                            mensaje = "Reacción actualizada",
                            accion = "actualizada",
                            data = new
                            {
                                idReaccion = reaccionExistente.IdReaccion,
                                tipo = reaccionExistente.Tipo
                            }
                        });
                    }
                }

                // Crear nueva reacción
                var nuevaReaccion = new Reaccione
                {
                    Tipo = request.Tipo,
                    FechaReaccion = DateTime.UtcNow,
                    IdUsuario = userId,
                    IdPublicacion = request.IdPublicacion,
                    IdComentario = request.IdComentario
                };

                await _dbMasterContext.Reacciones.AddAsync(nuevaReaccion);
                await _dbMasterContext.SaveChangesAsync();

                _logger.LogInformation("Reacción creada exitosamente. ID: {IdReaccion}", nuevaReaccion.IdReaccion);

                return Ok(new
                {
                    isSuccess = true,
                    mensaje = "Reacción creada exitosamente",
                    accion = "creada",
                    data = new
                    {
                        idReaccion = nuevaReaccion.IdReaccion,
                        tipo = nuevaReaccion.Tipo
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear/actualizar reacción");
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al procesar la reacción"
                });
            }
        }

        /// <summary>
        /// Elimina una reacción
        /// </summary>
        [HttpDelete]
        [Route("Eliminar/{id}")]
        public async Task<IActionResult> EliminarReaccion(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst("idUsuario")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { isSuccess = false, mensaje = "No se pudo validar el usuario" });
                }

                var reaccion = await _dbMasterContext.Reacciones.FindAsync(id);
                if (reaccion == null)
                {
                    return NotFound(new { isSuccess = false, mensaje = "La reacción no existe" });
                }

                // Verificar que el usuario es el dueño de la reacción
                if (reaccion.IdUsuario != userId)
                {
                    return Forbid();
                }

                _dbMasterContext.Reacciones.Remove(reaccion);
                await _dbMasterContext.SaveChangesAsync();

                _logger.LogInformation("Reacción eliminada exitosamente. ID: {IdReaccion}", id);

                return Ok(new { isSuccess = true, mensaje = "Reacción eliminada correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar reacción {Id}", id);
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al eliminar la reacción"
                });
            }
        }
    }

    // DTOs para las peticiones
    public class CrearReaccionRequest
    {
        public required string Tipo { get; set; } // Like, Dislike, Corazón, etc.
        public int? IdPublicacion { get; set; }
        public int? IdComentario { get; set; }
    }
}

