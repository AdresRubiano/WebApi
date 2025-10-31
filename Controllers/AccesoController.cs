
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
        private readonly Utilidades _utilidades;
        private readonly ILogger<AccesoController> _logger;

        public AccesoController(
            DbMasterContext dbMasterContext, 
            Utilidades utilidades,
            ILogger<AccesoController> logger)
        {
            _dbMasterContext = dbMasterContext;
            _utilidades = utilidades;
            _logger = logger;
        }
        [HttpPost]
        [Route("Registrarse")]
        public async Task<IActionResult> Registrarse(UsuarioDTO objeto)
        {
            try
            {
                // Validaciones básicas
                if (objeto == null)
                {
                    return BadRequest(new { isSuccess = false, mensaje = "Los datos del usuario son requeridos" });
                }

                if (string.IsNullOrWhiteSpace(objeto.Nombre))
                {
                    return BadRequest(new { isSuccess = false, mensaje = "El nombre es obligatorio" });
                }

                if (string.IsNullOrWhiteSpace(objeto.Username))
                {
                    return BadRequest(new { isSuccess = false, mensaje = "El nombre de usuario es obligatorio" });
                }

                if (string.IsNullOrWhiteSpace(objeto.correo))
                {
                    return BadRequest(new { isSuccess = false, mensaje = "El correo electrónico es obligatorio" });
                }

                if (string.IsNullOrWhiteSpace(objeto.PasswordHash))
                {
                    return BadRequest(new { isSuccess = false, mensaje = "La contraseña es obligatoria" });
                }

                // Validar formato de correo básico
                if (!objeto.correo.Contains("@") || !objeto.correo.Contains("."))
                {
                    return BadRequest(new { isSuccess = false, mensaje = "El formato del correo electrónico no es válido" });
                }

                // Validar longitud mínima de contraseña
                if (objeto.PasswordHash.Length < 6)
                {
                    return BadRequest(new { isSuccess = false, mensaje = "La contraseña debe tener al menos 6 caracteres" });
                }

                // Verificar si el correo ya existe
                var correoExiste = await _dbMasterContext.Usuarios
                    .AnyAsync(u => u.Correo == objeto.correo);

                if (correoExiste)
                {
                    return BadRequest(new { isSuccess = false, mensaje = "El correo electrónico ya está registrado" });
                }

                // Verificar si el username ya existe
                var usernameExiste = await _dbMasterContext.Usuarios
                    .AnyAsync(u => u.Username == objeto.Username);

                if (usernameExiste)
                {
                    return BadRequest(new { isSuccess = false, mensaje = "El nombre de usuario ya está en uso" });
                }

                var modeloUsuario = new Usuario
                {
                    Nombre = objeto.Nombre.Trim(),
                    Username = objeto.Username.Trim(),
                    Correo = objeto.correo.Trim().ToLower(),
                    PasswordHash = _utilidades.EncriptarSHA256(objeto.PasswordHash)
                };

                await _dbMasterContext.Usuarios.AddAsync(modeloUsuario);
                await _dbMasterContext.SaveChangesAsync();

                if (modeloUsuario.IdUsuario != 0)
                {
                    _logger.LogInformation("Usuario registrado exitosamente. ID: {IdUsuario}, Correo: {Correo}", 
                        modeloUsuario.IdUsuario, modeloUsuario.Correo);
                    return Ok(new { 
                        isSuccess = true, 
                        mensaje = "Usuario registrado exitosamente",
                        data = new { idUsuario = modeloUsuario.IdUsuario }
                    });
                }
                else
                {
                    return BadRequest(new { isSuccess = false, mensaje = "No se pudo registrar el usuario" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar usuario");
                return StatusCode(StatusCodes.Status500InternalServerError, new 
                { 
                    isSuccess = false, 
                    mensaje = "Error interno del servidor al registrar el usuario" 
                });
            }
        }
        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> Login(LoginDTO objeto)
        {
            try
            {
                // Validaciones básicas
                if (objeto == null)
                {
                    return BadRequest(new { isSuccess = false, mensaje = "Las credenciales son requeridas" });
                }

                if (string.IsNullOrWhiteSpace(objeto.Correo))
                {
                    return BadRequest(new { isSuccess = false, mensaje = "El correo electrónico es obligatorio" });
                }

                if (string.IsNullOrWhiteSpace(objeto.PasswordHash))
                {
                    return BadRequest(new { isSuccess = false, mensaje = "La contraseña es obligatoria" });
                }

                var passwordHashEncriptado = _utilidades.EncriptarSHA256(objeto.PasswordHash);

                var usuarioEncontrado = await _dbMasterContext.Usuarios
                    .Where(u => u.Correo == objeto.Correo.ToLower().Trim() && 
                                u.PasswordHash == passwordHashEncriptado)
                    .FirstOrDefaultAsync();

                if (usuarioEncontrado == null)
                {
                    _logger.LogWarning("Intento de login fallido para el correo: {Correo}", objeto.Correo);
                    return Unauthorized(new
                    {
                        isSuccess = false,
                        mensaje = "Credenciales incorrectas"
                    });
                }

                var token = _utilidades.GenerarJWT(usuarioEncontrado);
                _logger.LogInformation("Login exitoso para el usuario ID: {IdUsuario}, Correo: {Correo}", 
                    usuarioEncontrado.IdUsuario, usuarioEncontrado.Correo);

                return Ok(new 
                { 
                    isSuccess = true, 
                    mensaje = "Inicio de sesión exitoso",
                    token = token,
                    data = new 
                    {
                        idUsuario = usuarioEncontrado.IdUsuario,
                        nombre = usuarioEncontrado.Nombre,
                        correo = usuarioEncontrado.Correo,
                        username = usuarioEncontrado.Username
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al realizar login");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    isSuccess = false,
                    mensaje = "Error interno del servidor al iniciar sesión"
                });
            }
        }

    }
}
