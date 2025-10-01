using System;
using System.Collections.Generic;

namespace WebApi.Models;

public partial class Reaccione
{
    public int IdReaccion { get; set; }

    public string Tipo { get; set; } = null!;

    public DateTime? FechaReaccion { get; set; }

    public int IdUsuario { get; set; }

    public int? IdPublicacion { get; set; }

    public int? IdComentario { get; set; }

    public virtual Comentario? IdComentarioNavigation { get; set; }

    public virtual Publicacione? IdPublicacionNavigation { get; set; }

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;
}
