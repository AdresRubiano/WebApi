using System;
using System.Collections.Generic;

namespace WebApi.Models;

public partial class Seguidore
{
    public int IdUsuario { get; set; }

    public int IdSeguido { get; set; }

    public DateTime? FechaSeguimiento { get; set; }

    public virtual Usuario IdSeguidoNavigation { get; set; } = null!;

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;
}
