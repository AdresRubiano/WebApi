using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using WebApi.Models;
namespace WebApi.Custom
{
    public class Utilidades
    {
        private readonly IConfiguration _configuration;
        public Utilidades(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public string EncriptarSHA256(string text)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(text));
                StringBuilder builder = new StringBuilder();
                for(int i  = 0;  i < bytes.Length; i++)
                {

                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
                
        }
        public string GenerarJWT(Usuario Modelo)
        {
            var userClaims = new[]      
            {
                   
                new Claim( ClaimTypes.NameIdentifier, Modelo.IdUsuario.ToString() ),
                new Claim(ClaimTypes.Email,Modelo.Correo!),
                new Claim("idUsuario", Modelo.IdUsuario.ToString())
            };
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var credentials = new SigningCredentials(securityKey,SecurityAlgorithms.HmacSha256Signature);

            //create dalle del token
            var jwtConfig = new JwtSecurityToken(
                claims: userClaims,
                expires:DateTime.UtcNow.AddMinutes(10),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(jwtConfig);
            
               
            

            
        }
    }
}
