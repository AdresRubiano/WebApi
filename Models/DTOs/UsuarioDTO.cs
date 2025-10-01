namespace WebApi.Models.DTOs
{
    public class UsuarioDTO
    {
        public string Nombre { get; set; }
        public string Username { get; set; }
        public string correo { get; set; }
        public string PasswordHash { get; set; }
    }
}
