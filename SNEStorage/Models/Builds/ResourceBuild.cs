using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class Resource
{
    public static void Build(EntityTypeBuilder<Resource> entity)
    {
        entity.ToTable("resource");

        entity.HasKey(p => p.Id)
            .HasName("PK_Resource");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
            .HasColumnType("text")
            .HasColumnName("name");
        entity.Property(e => e.Description)
            .HasColumnType("text")
            .HasColumnName("description");
        entity.Property(e => e.Usage)
            .HasColumnType("text")
            .HasColumnName("usage");
        entity.Property(e => e.Version)
            .HasColumnType("text")
            .HasColumnName("version");

        entity.Property(e => e.Downloads)
            .HasColumnName("downloads");

        entity.Property(e => e.VideogameId)
            .HasColumnName("videogame_id");
        entity.HasOne(d => d.Videogame).WithMany(p => p.Resources)
            .HasForeignKey(d => d.VideogameId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_videogame");

        entity.Property(e => e.ResourceTypeId)
            .HasColumnName("resource_type_id");
        entity.HasOne(d => d.ResourceType).WithMany(p => p.Resources)
            .HasForeignKey(d => d.ResourceTypeId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_resource_type");

        entity.Property(e => e.FileId)
            .HasColumnName("file_id");
        entity.HasOne(d => d.File).WithMany(p => p.Resources)
            .HasForeignKey(d => d.FileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_file");

        entity.Property(e => e.SubmitterUserId)
            .HasColumnName("submitter_user_id");
        entity.HasOne(d => d.SubmitterUser).WithMany(p => p.Resources)
            .HasForeignKey(d => d.SubmitterUserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_submitter_user");

        entity.Property(e => e.ScoreId)
            .HasColumnName("score_id");
        entity.HasOne(d => d.Score).WithMany(p => p.Resources)
            .HasForeignKey(d => d.ScoreId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_score");

    }
}
