using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Models;

namespace WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SeguidoresController : ControllerBase
    {
        private readonly DbMasterContext _dbMasterContext;
        private readonly ILogger<SeguidoresController> _logger;

        public SeguidoresController(
            DbMasterContext dbMasterContext,
            ILogger<SeguidoresController> logger)
        {
            _dbMasterContext = dbMasterContext;
            _logger = logger;
        }

        /// <summary>
        /// Lista los seguidores de un usuario
        /// </summary>
        [HttpGet]
        [Route("Seguidores/{idUsuario}")]
        [AllowAnonymous]
        public async Task<IActionResult> ListarSeguidores(int idUsuario)
        {
            try
            {
                var seguidores = await _dbMasterContext.Seguidores
                    .Where(s => s.IdSeguido == idUsuario)
                    .Include(s => s.IdUsuarioNavigation)
                    .OrderByDescending(s => s.FechaSeguimiento)
                    .Select(s => new
                    {
                        s.IdUsuario,
                        Usuario = new
                        {
                            s.IdUsuarioNavigation.IdUsuario,
                            s.IdUsuarioNavigation.Nombre,
                            s.IdUsuarioNavigation.Username,
                            s.IdUsuarioNavigation.FotoPerfil,
                            s.IdUsuarioNavigation.Bio
                        },
                        s.FechaSeguimiento
                    })
                    .ToListAsync();

                return Ok(new { isSuccess = true, data = seguidores, total = seguidores.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar seguidores del usuario {IdUsuario}", idUsuario);
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al listar los seguidores"
                });
            }
        }

        /// <summary>
        /// Lista los usuarios que sigue un usuario
        /// </summary>
        [HttpGet]
        [Route("Siguiendo/{idUsuario}")]
        [AllowAnonymous]
        public async Task<IActionResult> ListarSiguiendo(int idUsuario)
        {
            try
            {
                var siguiendo = await _dbMasterContext.Seguidores
                    .Where(s => s.IdUsuario == idUsuario)
                    .Include(s => s.IdSeguidoNavigation)
                    .OrderByDescending(s => s.FechaSeguimiento)
                    .Select(s => new
                    {
                        s.IdSeguido,
                        Usuario = new
                        {
                            s.IdSeguidoNavigation.IdUsuario,
                            s.IdSeguidoNavigation.Nombre,
                            s.IdSeguidoNavigation.Username,
                            s.IdSeguidoNavigation.FotoPerfil,
                            s.IdSeguidoNavigation.Bio
                        },
                        s.FechaSeguimiento
                    })
                    .ToListAsync();

                return Ok(new { isSuccess = true, data = siguiendo, total = siguiendo.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar usuarios que sigue el usuario {IdUsuario}", idUsuario);
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al listar los usuarios que sigue"
                });
            }
        }

        /// <summary>
        /// Obtiene estadísticas de seguidores de un usuario
        /// </summary>
        [HttpGet]
        [Route("Estadisticas/{idUsuario}")]
        [AllowAnonymous]
        public async Task<IActionResult> ObtenerEstadisticas(int idUsuario)
        {
            try
            {
                var totalSeguidores = await _dbMasterContext.Seguidores
                    .CountAsync(s => s.IdSeguido == idUsuario);

                var totalSiguiendo = await _dbMasterContext.Seguidores
                    .CountAsync(s => s.IdUsuario == idUsuario);

                var totalPublicaciones = await _dbMasterContext.Publicaciones
                    .CountAsync(p => p.IdUsuario == idUsuario && p.Estado == "Publicado");

                return Ok(new
                {
                    isSuccess = true,
                    data = new
                    {
                        totalSeguidores,
                        totalSiguiendo,
                        totalPublicaciones
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas del usuario {IdUsuario}", idUsuario);
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al obtener las estadísticas"
                });
            }
        }

        /// <summary>
        /// Verifica si el usuario autenticado sigue a otro usuario
        /// </summary>
        [HttpGet]
        [Route("VerificarSeguimiento/{idUsuarioSeguido}")]
        public async Task<IActionResult> VerificarSeguimiento(int idUsuarioSeguido)
        {
            try
            {
                var userIdClaim = User.FindFirst("idUsuario")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { isSuccess = false, mensaje = "No se pudo validar el usuario" });
                }

                if (userId == idUsuarioSeguido)
                {
                    return BadRequest(new { isSuccess = false, mensaje = "No puedes seguirte a ti mismo" });
                }

                var sigue = await _dbMasterContext.Seguidores
                    .AnyAsync(s => s.IdUsuario == userId && s.IdSeguido == idUsuarioSeguido);

                return Ok(new
                {
                    isSuccess = true,
                    sigue,
                    mensaje = sigue ? "Ya sigues a este usuario" : "No sigues a este usuario"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar seguimiento");
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al verificar el seguimiento"
                });
            }
        }

        /// <summary>
        /// Sigue a un usuario
        /// </summary>
        [HttpPost]
        [Route("Seguir/{idUsuarioSeguido}")]
        public async Task<IActionResult> SeguirUsuario(int idUsuarioSeguido)
        {
            try
            {
                var userIdClaim = User.FindFirst("idUsuario")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { isSuccess = false, mensaje = "No se pudo validar el usuario" });
                }

                if (userId == idUsuarioSeguido)
                {
                    return BadRequest(new { isSuccess = false, mensaje = "No puedes seguirte a ti mismo" });
                }

                // Verificar que el usuario a seguir existe
                var usuarioSeguido = await _dbMasterContext.Usuarios.FindAsync(idUsuarioSeguido);
                if (usuarioSeguido == null)
                {
                    return NotFound(new { isSuccess = false, mensaje = "El usuario a seguir no existe" });
                }

                // Verificar si ya lo sigue
                var yaSigue = await _dbMasterContext.Seguidores
                    .AnyAsync(s => s.IdUsuario == userId && s.IdSeguido == idUsuarioSeguido);

                if (yaSigue)
                {
                    return BadRequest(new { isSuccess = false, mensaje = "Ya sigues a este usuario" });
                }

                var nuevoSeguidor = new Seguidore
                {
                    IdUsuario = userId,
                    IdSeguido = idUsuarioSeguido,
                    FechaSeguimiento = DateTime.UtcNow
                };

                await _dbMasterContext.Seguidores.AddAsync(nuevoSeguidor);
                await _dbMasterContext.SaveChangesAsync();

                _logger.LogInformation("Usuario {IdUsuario} ahora sigue a {IdUsuarioSeguido}", userId, idUsuarioSeguido);

                return Ok(new
                {
                    isSuccess = true,
                    mensaje = "Ahora sigues a este usuario",
                    data = new
                    {
                        idUsuario = userId,
                        idUsuarioSeguido = idUsuarioSeguido,
                        fechaSeguimiento = nuevoSeguidor.FechaSeguimiento
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al seguir usuario");
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al seguir al usuario"
                });
            }
        }

        /// <summary>
        /// Deja de seguir a un usuario
        /// </summary>
        [HttpDelete]
        [Route("DejarDeSeguir/{idUsuarioSeguido}")]
        public async Task<IActionResult> DejarDeSeguirUsuario(int idUsuarioSeguido)
        {
            try
            {
                var userIdClaim = User.FindFirst("idUsuario")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { isSuccess = false, mensaje = "No se pudo validar el usuario" });
                }

                var seguimiento = await _dbMasterContext.Seguidores
                    .FirstOrDefaultAsync(s => s.IdUsuario == userId && s.IdSeguido == idUsuarioSeguido);

                if (seguimiento == null)
                {
                    return NotFound(new { isSuccess = false, mensaje = "No sigues a este usuario" });
                }

                _dbMasterContext.Seguidores.Remove(seguimiento);
                await _dbMasterContext.SaveChangesAsync();

                _logger.LogInformation("Usuario {IdUsuario} dejó de seguir a {IdUsuarioSeguido}", userId, idUsuarioSeguido);

                return Ok(new { isSuccess = true, mensaje = "Has dejado de seguir a este usuario" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al dejar de seguir usuario");
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al dejar de seguir al usuario"
                });
            }
        }
    }
}

