using System;
using System.Collections.Generic;

namespace WebApi.Models;

public partial class Publicacione
{
    public int IdPublicacion { get; set; }

    public string Titulo { get; set; } = null!;

    public string Contenido { get; set; } = null!;

    public string? Etiquetas { get; set; }

    public string? ImagenUrl { get; set; }

    public string? Estado { get; set; }

    public DateTime? FechaPublicacion { get; set; }

    public DateTime? FechaActualizacion { get; set; }

    public int IdUsuario { get; set; }

    public int? IdCategoria { get; set; }

    public virtual ICollection<Comentario> Comentarios { get; set; } = new List<Comentario>();

    public virtual Categoria? IdCategoriaNavigation { get; set; }

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;

    public virtual ICollection<Reaccione> Reacciones { get; set; } = new List<Reaccione>();
}
