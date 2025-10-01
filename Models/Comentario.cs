using System;
using System.Collections.Generic;

namespace WebApi.Models;

public partial class Comentario
{
    public int IdComentario { get; set; }

    public string Comentario1 { get; set; } = null!;

    public DateTime? FechaComentario { get; set; }

    public bool? Editado { get; set; }

    public int IdUsuario { get; set; }

    public int IdPublicacion { get; set; }

    public virtual Publicacione IdPublicacionNavigation { get; set; } = null!;

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;

    public virtual ICollection<Reaccione> Reacciones { get; set; } = new List<Reaccione>();
}
