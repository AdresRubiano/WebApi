using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Models;
using WebApi.Models.DTOs;

namespace WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] 
    public class PublicacionesController : ControllerBase
    {
        private readonly DbMasterContext _dbMasterContext;

        public PublicacionesController(DbMasterContext dbMasterContext)
        {
            _dbMasterContext = dbMasterContext;
        }

        [HttpPost]
        [Route("Crear")]
        public async Task<IActionResult> CrearPublicacion([FromBody] PublicacionDTO objeto)
        {
            if (string.IsNullOrEmpty(objeto.Titulo) || string.IsNullOrEmpty(objeto.Contenido))
            {
                return BadRequest(new { isSuccess = false, mensaje = "El título y contenido son obligatorios" });
            }

            var userId = User.FindFirst("idUsuario")?.Value;
            if (userId == null)
            {
                return Unauthorized(new { isSuccess = false, mensaje = "No se pudo validar el usuario" });
            }

            var usuarioExiste = await _dbMasterContext.Usuarios.FindAsync(int.Parse(userId));
            if (usuarioExiste == null)
            {
                return BadRequest(new { isSuccess = false, mensaje = "El usuario no existe" });
            }

            if (objeto.IdCategoria.HasValue)
            {
                var categoriaExiste = await _dbMasterContext.Categorias.FindAsync(objeto.IdCategoria.Value);
                if (categoriaExiste == null)
                {
                    return BadRequest(new { isSuccess = false, mensaje = "La categoría no existe" });
                }
            }

            var nuevaPublicacion = new Publicacione
            {
                Titulo = objeto.Titulo,
                Contenido = objeto.Contenido,
                Etiquetas = objeto.Etiquetas,
                ImagenUrl = objeto.ImagenUrl,
                Estado = "Publicado",
                FechaPublicacion = DateTime.Now,
                FechaActualizacion = DateTime.Now,
                IdUsuario = int.Parse(userId), 

                IdCategoria = objeto.IdCategoria
            };

            await _dbMasterContext.Publicaciones.AddAsync(nuevaPublicacion);
            await _dbMasterContext.SaveChangesAsync();

            return Ok(new { isSuccess = true, idPublicacion = nuevaPublicacion.IdPublicacion });

        }
        [HttpGet]
        [Route("Listar")]
        public async Task<IActionResult> ListarPublicaciones()
        {
            var publicaciones = await _dbMasterContext.Publicaciones
                .Include(p => p.IdUsuarioNavigation)  // incluir datos del usuario
                .Include(p => p.IdCategoriaNavigation) // incluir categoría
                .Select(p => new
                {
                    p.IdPublicacion,
                    p.Titulo,
                    p.Contenido,
                    p.Etiquetas,
                    p.ImagenUrl,
                    p.Estado,
                    p.FechaPublicacion,
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

            return Ok(new { isSuccess = true, publicaciones });
        }

        // GET: Listar publicaciones SOLO del usuario autenticado
        [HttpGet]
        [Route("MisPublicaciones")]
        public async Task<IActionResult> MisPublicaciones()
        {
            var userId = User.FindFirst("idUsuario")?.Value;
            if (userId == null)
            {
                return Unauthorized(new { isSuccess = false, mensaje = "No se pudo validar el usuario" });
            }

            var publicaciones = await _dbMasterContext.Publicaciones
                .Where(p => p.IdUsuario == int.Parse(userId))
                .Select(p => new {
                    p.IdPublicacion,
                    p.Titulo,
                    p.Contenido,
                    p.Etiquetas,
                    p.ImagenUrl,
                    p.Estado,
                    p.FechaPublicacion
                })
                .ToListAsync();

            return Ok(new { isSuccess = true, publicaciones });
        }
        [HttpPut]
        [Route("Actualizar/{id}")]
        public async Task<IActionResult> ActualizarPublicacion(int id, [FromBody] PublicacionDTO objeto)
        {
            var userId = User.FindFirst("idUsuario")?.Value;
            if (userId == null)
                return Unauthorized(new { isSuccess = false, mensaje = "No se pudo validar el usuario" });

            var publicacion = await _dbMasterContext.Publicaciones.FindAsync(id);
            if (publicacion == null)
                return NotFound(new { isSuccess = false, mensaje = "La publicación no existe" });

            if (publicacion.IdUsuario != int.Parse(userId))
                return Forbid();

            // Actualizar solo campos editables
            publicacion.Titulo = objeto.Titulo ?? publicacion.Titulo;
            publicacion.Contenido = objeto.Contenido ?? publicacion.Contenido;
            publicacion.Etiquetas = objeto.Etiquetas ?? publicacion.Etiquetas;
            publicacion.ImagenUrl = objeto.ImagenUrl ?? publicacion.ImagenUrl;
            publicacion.IdCategoria = objeto.IdCategoria ?? publicacion.IdCategoria;
            publicacion.FechaActualizacion = DateTime.Now;

            _dbMasterContext.Publicaciones.Update(publicacion);
            await _dbMasterContext.SaveChangesAsync();

            return Ok(new { isSuccess = true, mensaje = "Publicación actualizada correctamente" });
        }

        // DELETE: Eliminar publicación
        [HttpDelete]
        [Route("Eliminar/{id}")]
        public async Task<IActionResult> EliminarPublicacion(int id)
        {
            var userId = User.FindFirst("idUsuario")?.Value;
            if (userId == null)
                return Unauthorized(new { isSuccess = false, mensaje = "No se pudo validar el usuario" });

            var publicacion = await _dbMasterContext.Publicaciones.FindAsync(id);
            if (publicacion == null)
                return NotFound(new { isSuccess = false, mensaje = "La publicación no existe" });

            if (publicacion.IdUsuario != int.Parse(userId))
                return Forbid(); // ❌ El usuario no es dueño de la publicación

            _dbMasterContext.Publicaciones.Remove(publicacion);
            await _dbMasterContext.SaveChangesAsync();

            return Ok(new { isSuccess = true, mensaje = "Publicación eliminada correctamente" });
        }
    }
}
