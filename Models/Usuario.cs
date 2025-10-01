using System;
using System.Collections.Generic;

namespace WebApi.Models;

public partial class Usuario
{
    public int IdUsuario { get; set; }

    public string Nombre { get; set; } = null!;

    public string Username { get; set; } = null!;

    public string Correo { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public int? Edad { get; set; }

    public string? Rol { get; set; }

    public string? FotoPerfil { get; set; }

    public string? Bio { get; set; }

    public bool? Activo { get; set; }

    public DateTime? FechaCreacion { get; set; }

    public DateTime? FechaActualizacion { get; set; }

    public virtual ICollection<Comentario> Comentarios { get; set; } = new List<Comentario>();

    public virtual ICollection<Publicacione> Publicaciones { get; set; } = new List<Publicacione>();

    public virtual ICollection<Reaccione> Reacciones { get; set; } = new List<Reaccione>();

    public virtual ICollection<Seguidore> SeguidoreIdSeguidoNavigations { get; set; } = new List<Seguidore>();

    public virtual ICollection<Seguidore> SeguidoreIdUsuarioNavigations { get; set; } = new List<Seguidore>();
}
