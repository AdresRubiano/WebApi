using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Models;

namespace WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriasController : ControllerBase
    {
        private readonly DbMasterContext _dbMasterContext;
        private readonly ILogger<CategoriasController> _logger;

        public CategoriasController(
            DbMasterContext dbMasterContext,
            ILogger<CategoriasController> logger)
        {
            _dbMasterContext = dbMasterContext;
            _logger = logger;
        }

        /// <summary>
        /// Lista todas las categorías
        /// </summary>
        [HttpGet]
        [Route("Listar")]
        [AllowAnonymous]
        public async Task<IActionResult> ListarCategorias()
        {
            try
            {
                var categorias = await _dbMasterContext.Categorias
                    .OrderBy(c => c.Nombre)
                    .Select(c => new
                    {
                        c.IdCategoria,
                        c.Nombre,
                        c.Descripcion,
                        c.FechaCreacion,
                        TotalPublicaciones = c.Publicaciones.Count
                    })
                    .ToListAsync();

                return Ok(new { isSuccess = true, data = categorias, total = categorias.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar categorías");
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al listar las categorías"
                });
            }
        }

        /// <summary>
        /// Obtiene una categoría por ID
        /// </summary>
        [HttpGet]
        [Route("Obtener/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> ObtenerCategoria(int id)
        {
            try
            {
                var categoria = await _dbMasterContext.Categorias
                    .Where(c => c.IdCategoria == id)
                    .Select(c => new
                    {
                        c.IdCategoria,
                        c.Nombre,
                        c.Descripcion,
                        c.FechaCreacion,
                        TotalPublicaciones = c.Publicaciones.Count,
                        Publicaciones = c.Publicaciones.Select(p => new
                        {
                            p.IdPublicacion,
                            p.Titulo,
                            p.FechaPublicacion
                        }).ToList()
                    })
                    .FirstOrDefaultAsync();

                if (categoria == null)
                {
                    return NotFound(new { isSuccess = false, mensaje = "La categoría no existe" });
                }

                return Ok(new { isSuccess = true, data = categoria });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener categoría {Id}", id);
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al obtener la categoría"
                });
            }
        }

        /// <summary>
        /// Crea una nueva categoría (solo administradores)
        /// </summary>
        [HttpPost]
        [Route("Crear")]
        [Authorize]
        public async Task<IActionResult> CrearCategoria([FromBody] CrearCategoriaRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Nombre))
                {
                    return BadRequest(new { isSuccess = false, mensaje = "El nombre de la categoría es obligatorio" });
                }

                if (request.Nombre.Length > 100)
                {
                    return BadRequest(new { isSuccess = false, mensaje = "El nombre no puede exceder 100 caracteres" });
                }

                // Verificar si el nombre ya existe
                var nombreExiste = await _dbMasterContext.Categorias
                    .AnyAsync(c => c.Nombre.ToLower() == request.Nombre.Trim().ToLower());

                if (nombreExiste)
                {
                    return BadRequest(new { isSuccess = false, mensaje = "Ya existe una categoría con ese nombre" });
                }

                var nuevaCategoria = new Categoria
                {
                    Nombre = request.Nombre.Trim(),
                    Descripcion = request.Descripcion?.Trim(),
                    FechaCreacion = DateTime.UtcNow
                };

                await _dbMasterContext.Categorias.AddAsync(nuevaCategoria);
                await _dbMasterContext.SaveChangesAsync();

                _logger.LogInformation("Categoría creada exitosamente. ID: {IdCategoria}", nuevaCategoria.IdCategoria);

                return Ok(new
                {
                    isSuccess = true,
                    mensaje = "Categoría creada exitosamente",
                    data = new
                    {
                        idCategoria = nuevaCategoria.IdCategoria,
                        nombre = nuevaCategoria.Nombre,
                        descripcion = nuevaCategoria.Descripcion
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear categoría");
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al crear la categoría"
                });
            }
        }

        /// <summary>
        /// Actualiza una categoría existente (solo administradores)
        /// </summary>
        [HttpPut]
        [Route("Actualizar/{id}")]
        [Authorize]
        public async Task<IActionResult> ActualizarCategoria(int id, [FromBody] ActualizarCategoriaRequest request)
        {
            try
            {
                var categoria = await _dbMasterContext.Categorias.FindAsync(id);
                if (categoria == null)
                {
                    return NotFound(new { isSuccess = false, mensaje = "La categoría no existe" });
                }

                if (request == null)
                {
                    return BadRequest(new { isSuccess = false, mensaje = "Los datos son requeridos" });
                }

                // Actualizar nombre si se proporciona
                if (!string.IsNullOrWhiteSpace(request.Nombre))
                {
                    if (request.Nombre.Length > 100)
                    {
                        return BadRequest(new { isSuccess = false, mensaje = "El nombre no puede exceder 100 caracteres" });
                    }

                    // Verificar si el nuevo nombre ya existe (excepto la categoría actual)
                    var nombreExiste = await _dbMasterContext.Categorias
                        .AnyAsync(c => c.Nombre.ToLower() == request.Nombre.Trim().ToLower() && c.IdCategoria != id);

                    if (nombreExiste)
                    {
                        return BadRequest(new { isSuccess = false, mensaje = "Ya existe una categoría con ese nombre" });
                    }

                    categoria.Nombre = request.Nombre.Trim();
                }

                // Actualizar descripción si se proporciona
                if (request.Descripcion != null)
                {
                    categoria.Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim();
                }

                _dbMasterContext.Categorias.Update(categoria);
                await _dbMasterContext.SaveChangesAsync();

                _logger.LogInformation("Categoría actualizada exitosamente. ID: {IdCategoria}", id);

                return Ok(new
                {
                    isSuccess = true,
                    mensaje = "Categoría actualizada correctamente",
                    data = new
                    {
                        idCategoria = categoria.IdCategoria,
                        nombre = categoria.Nombre,
                        descripcion = categoria.Descripcion
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar categoría {Id}", id);
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al actualizar la categoría"
                });
            }
        }

        /// <summary>
        /// Elimina una categoría (solo administradores)
        /// </summary>
        [HttpDelete]
        [Route("Eliminar/{id}")]
        [Authorize]
        public async Task<IActionResult> EliminarCategoria(int id)
        {
            try
            {
                var categoria = await _dbMasterContext.Categorias
                    .Include(c => c.Publicaciones)
                    .FirstOrDefaultAsync(c => c.IdCategoria == id);

                if (categoria == null)
                {
                    return NotFound(new { isSuccess = false, mensaje = "La categoría no existe" });
                }

                // Verificar si tiene publicaciones asociadas
                if (categoria.Publicaciones.Any())
                {
                    return BadRequest(new
                    {
                        isSuccess = false,
                        mensaje = "No se puede eliminar la categoría porque tiene publicaciones asociadas"
                    });
                }

                _dbMasterContext.Categorias.Remove(categoria);
                await _dbMasterContext.SaveChangesAsync();

                _logger.LogInformation("Categoría eliminada exitosamente. ID: {IdCategoria}", id);

                return Ok(new { isSuccess = true, mensaje = "Categoría eliminada correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar categoría {Id}", id);
                return StatusCode(500, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al eliminar la categoría"
                });
            }
        }
    }

    // DTOs para las peticiones
    public class CrearCategoriaRequest
    {
        public required string Nombre { get; set; }
        public string? Descripcion { get; set; }
    }

    public class ActualizarCategoriaRequest
    {
        public string? Nombre { get; set; }
        public string? Descripcion { get; set; }
    }
}

