namespace WebApi.Models.DTOs
{
    public class UsuarioDTO
    {
        public required string Nombre { get; set; }
        public required string Username { get; set; }
        public required string correo { get; set; }
        public required string PasswordHash { get; set; }
    }
}
