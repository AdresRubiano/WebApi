namespace WebApi.Models.DTOs
{
    public class LoginDTO
    {
        public required string Correo { get; set; }
        public required string PasswordHash { get; set; }
    }
}
