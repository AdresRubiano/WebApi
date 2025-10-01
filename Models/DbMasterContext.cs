using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Models;

public partial class DbMasterContext : DbContext
{
    public DbMasterContext()
    {
    }

    public DbMasterContext(DbContextOptions<DbMasterContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Categoria> Categorias { get; set; }

    public virtual DbSet<Comentario> Comentarios { get; set; }

    public virtual DbSet<Publicacione> Publicaciones { get; set; }

    public virtual DbSet<Reaccione> Reacciones { get; set; }

    public virtual DbSet<Seguidore> Seguidores { get; set; }

    public virtual DbSet<Usuario> Usuarios { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // vacío, se configura desde Program.cs
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.HasKey(e => e.IdCategoria).HasName("PK__CATEGORI__4BD51FA5C636FEB5");

            entity.ToTable("CATEGORIAS");

            entity.HasIndex(e => e.Nombre, "UQ__CATEGORI__B21D0AB9F82AB80B").IsUnique();

            entity.Property(e => e.IdCategoria).HasColumnName("ID_CATEGORIA");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("DESCRIPCION");
            entity.Property(e => e.FechaCreacion)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("FECHA_CREACION");
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NOMBRE");
        });

        modelBuilder.Entity<Comentario>(entity =>
        {
            entity.HasKey(e => e.IdComentario).HasName("PK__COMENTAR__4B0815B19356FB94");

            entity.ToTable("COMENTARIOS");

            entity.Property(e => e.IdComentario).HasColumnName("ID_COMENTARIO");
            entity.Property(e => e.Comentario1)
                .IsUnicode(false)
                .HasColumnName("COMENTARIO");
            entity.Property(e => e.Editado)
                .HasDefaultValue(false)
                .HasColumnName("EDITADO");
            entity.Property(e => e.FechaComentario)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("FECHA_COMENTARIO");
            entity.Property(e => e.IdPublicacion).HasColumnName("ID_PUBLICACION");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_USUARIO");

            entity.HasOne(d => d.IdPublicacionNavigation).WithMany(p => p.Comentarios)
                .HasForeignKey(d => d.IdPublicacion)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__COMENTARI__ID_PU__4D94879B");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.Comentarios)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__COMENTARI__ID_US__4CA06362");
        });

        modelBuilder.Entity<Publicacione>(entity =>
        {
            entity.HasKey(e => e.IdPublicacion).HasName("PK__PUBLICAC__C7ABD9614B667CF2");

            entity.ToTable("PUBLICACIONES");

            entity.Property(e => e.IdPublicacion).HasColumnName("ID_PUBLICACION");
            entity.Property(e => e.Contenido)
                .IsUnicode(false)
                .HasColumnName("CONTENIDO");
            entity.Property(e => e.Estado)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Publicado")
                .HasColumnName("ESTADO");
            entity.Property(e => e.Etiquetas)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("ETIQUETAS");
            entity.Property(e => e.FechaActualizacion)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("FECHA_ACTUALIZACION");
            entity.Property(e => e.FechaPublicacion)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("FECHA_PUBLICACION");
            entity.Property(e => e.IdCategoria).HasColumnName("ID_CATEGORIA");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_USUARIO");
            entity.Property(e => e.ImagenUrl)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("IMAGEN_URL");
            entity.Property(e => e.Titulo)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("TITULO");

            entity.HasOne(d => d.IdCategoriaNavigation).WithMany(p => p.Publicaciones)
                .HasForeignKey(d => d.IdCategoria)
                .HasConstraintName("FK__PUBLICACI__ID_CA__47DBAE45");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.Publicaciones)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PUBLICACI__ID_US__46E78A0C");
        });

        modelBuilder.Entity<Reaccione>(entity =>
        {
            entity.HasKey(e => e.IdReaccion).HasName("PK__REACCION__907131FCB7B4A1D3");

            entity.ToTable("REACCIONES");

            entity.Property(e => e.IdReaccion).HasColumnName("ID_REACCION");
            entity.Property(e => e.FechaReaccion)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("FECHA_REACCION");
            entity.Property(e => e.IdComentario).HasColumnName("ID_COMENTARIO");
            entity.Property(e => e.IdPublicacion).HasColumnName("ID_PUBLICACION");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_USUARIO");
            entity.Property(e => e.Tipo)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("TIPO");

            entity.HasOne(d => d.IdComentarioNavigation).WithMany(p => p.Reacciones)
                .HasForeignKey(d => d.IdComentario)
                .HasConstraintName("FK__REACCIONE__ID_CO__534D60F1");

            entity.HasOne(d => d.IdPublicacionNavigation).WithMany(p => p.Reacciones)
                .HasForeignKey(d => d.IdPublicacion)
                .HasConstraintName("FK__REACCIONE__ID_PU__52593CB8");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.Reacciones)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__REACCIONE__ID_US__5165187F");
        });

        modelBuilder.Entity<Seguidore>(entity =>
        {
            entity.HasKey(e => new { e.IdUsuario, e.IdSeguido }).HasName("PK__SEGUIDOR__7D4261338BCCE07E");

            entity.ToTable("SEGUIDORES");

            entity.Property(e => e.IdUsuario).HasColumnName("ID_USUARIO");
            entity.Property(e => e.IdSeguido).HasColumnName("ID_SEGUIDO");
            entity.Property(e => e.FechaSeguimiento)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("FECHA_SEGUIMIENTO");

            entity.HasOne(d => d.IdSeguidoNavigation).WithMany(p => p.SeguidoreIdSeguidoNavigations)
                .HasForeignKey(d => d.IdSeguido)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__SEGUIDORE__ID_SE__5812160E");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.SeguidoreIdUsuarioNavigations)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__SEGUIDORE__ID_US__571DF1D5");
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.IdUsuario).HasName("PK__USUARIOS__91136B907D246058");

            entity.ToTable("USUARIOS");

            entity.HasIndex(e => e.Correo, "UQ__USUARIOS__264F33C81B1AD658").IsUnique();

            entity.HasIndex(e => e.Username, "UQ__USUARIOS__B15BE12E7913F01D").IsUnique();

            entity.Property(e => e.IdUsuario).HasColumnName("ID_USUARIO");
            entity.Property(e => e.Activo)
                .HasDefaultValue(true)
                .HasColumnName("ACTIVO");
            entity.Property(e => e.Bio)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("BIO");
            entity.Property(e => e.Correo)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("CORREO");
            entity.Property(e => e.Edad).HasColumnName("EDAD");
            entity.Property(e => e.FechaActualizacion)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("FECHA_ACTUALIZACION");
            entity.Property(e => e.FechaCreacion)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("FECHA_CREACION");
            entity.Property(e => e.FotoPerfil)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("FOTO_PERFIL");
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NOMBRE");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("PASSWORD_HASH");
            entity.Property(e => e.Rol)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Usuario")
                .HasColumnName("ROL");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("USERNAME");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
