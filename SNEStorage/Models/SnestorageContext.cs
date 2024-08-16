using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SNEStorage.Models;

public partial class SnestorageContext : IdentityDbContext
{
    public SnestorageContext()
    {
    }

    public SnestorageContext(DbContextOptions<SnestorageContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Resource> Resources { get; set; }

    public virtual DbSet<ResourceType> ResourceTypes { get; set; }

    public virtual DbSet<Videogame> Videogames { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Resource>(entity =>
        {
            entity.ToTable("resource");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.Downloads).HasColumnName("downloads");
            entity.Property(e => e.Name)
                .HasColumnType("text")
                .HasColumnName("name");
            entity.Property(e => e.ResourceTypeId).HasColumnName("resource_type_id");
            entity.Property(e => e.Url)
                .HasColumnType("text")
                .HasColumnName("url");
            entity.Property(e => e.Usage)
                .HasColumnType("text")
                .HasColumnName("usage");
            entity.Property(e => e.Version)
                .HasColumnType("text")
                .HasColumnName("version");
            entity.Property(e => e.VideogameId).HasColumnName("videogame_id");

            entity.HasOne(d => d.ResourceType).WithMany(p => p.Resources)
                .HasForeignKey(d => d.ResourceTypeId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_resource_resource_type");

            entity.HasOne(d => d.Videogame).WithMany(p => p.Resources)
                .HasForeignKey(d => d.VideogameId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_resource_videogame");
        });

        modelBuilder.Entity<ResourceType>(entity =>
        {
            entity.ToTable("resource_type");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasColumnType("text")
                .HasColumnName("name");
        });

        modelBuilder.Entity<Videogame>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_VideoGame");

            entity.ToTable("videogame");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasColumnType("text")
                .HasColumnName("name");
            entity.Property(e => e.Publisher)
                .HasColumnType("text")
                .HasColumnName("publisher");
            entity.Property(e => e.ReleaseDate).HasColumnName("release_date");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
