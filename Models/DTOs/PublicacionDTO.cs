namespace WebApi.Models.DTOs
{
    public class PublicacionDTO
    {
        public required string Titulo { get; set; }
        public required string Contenido { get; set; }
        public string? Etiquetas { get; set; }
        public string? ImagenUrl { get; set; }
        public int IdUsuario { get; set; }
        public int? IdCategoria { get; set; }
    }
}
