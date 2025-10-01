
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Custom;
using WebApi.Models;
using WebApi.Models.DTOs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace WebApi.Controllers
{
    [Route("api/[controller]")]
    [AllowAnonymous]
    [ApiController]
    public class AccesoController : ControllerBase
    {
        private readonly DbMasterContext _dbMasterContext;
        private readonly  Utilidades _utilidades;

        public AccesoController(DbMasterContext dbMasterContext, Utilidades utilidades)
        {
            _dbMasterContext = dbMasterContext;
            _utilidades = utilidades;
        }
        [HttpPost]
        [Route("Registrarse")]
        public async Task<IActionResult> Registrarse(UsuarioDTO objeto)
        {
            var modeloUsuario = new Usuario
            {
                Nombre = objeto.Nombre,
                Username= objeto.Username,
                Correo = objeto.correo,
                PasswordHash = _utilidades.EncriptarSHA256(objeto.PasswordHash)
            };
            await _dbMasterContext.Usuarios.AddAsync(modeloUsuario);
            await _dbMasterContext.SaveChangesAsync();
            if (modeloUsuario.IdUsuario != 0)
            {
                return StatusCode(StatusCodes.Status200OK, new { isSuccess = true });
            }
            else
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { isSuccess = false });
            }
        }
        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> Login(LoginDTO objeto)
        {
            var usuarioEncontrado = await _dbMasterContext.Usuarios
                                        .Where(u =>
                                        u.Correo == objeto.Correo && 
                                        u.PasswordHash == _utilidades.EncriptarSHA256(objeto.PasswordHash)
                                        ).FirstOrDefaultAsync();

            if (usuarioEncontrado == null)
            {
             
                return StatusCode(StatusCodes.Status401Unauthorized, new
                {
                    isSuccess = false,
                    mensaje = "Credenciales incorrectas"
                });
            }
            else
            {
                return StatusCode(StatusCodes.Status200OK, new { isSuccess = true, token = _utilidades.GenerarJWT(usuarioEncontrado)});
            }


        }

    }
}
