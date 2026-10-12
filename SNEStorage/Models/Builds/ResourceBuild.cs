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
            .HasColumnType("nvarchar(100)")
            .HasColumnName("name");
        entity.Property(e => e.Description)
            .HasColumnType("nvarchar(max)")
            .HasColumnName("description");

        entity.Property(e => e.CreatedAt)
            .HasColumnName("created_at");
        entity.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        entity.Property(e => e.IncludesPorn)
            .HasColumnName("includes_porn");
        entity.Property(e => e.IncludesGore)
            .HasColumnName("includes_gore");
        entity.Property(e => e.IncludesPolitics)
            .HasColumnName("includes_politics");
        entity.Property(e => e.IncludesSlurs)
            .HasColumnName("includes_slurs");
        entity.Property(e => e.IncludesSensitiveContent)
            .HasColumnName("includes_sensitive_content");

        entity.Property(e => e.VideogameId)
            .HasColumnName("videogame_id");
        entity.HasOne(d => d.Videogame).WithMany(p => p.Resources)
            .HasForeignKey(d => d.VideogameId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_videogame");

        entity.Property(e => e.ScoreId)
            .HasColumnName("score_id");
        entity.HasOne(d => d.Score).WithOne(p => p.Resource)
            .HasForeignKey<Resource>(d => d.ScoreId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_score");

        entity.Property(e => e.VisibilityId)
            .HasColumnName("visibility_id");
        entity.HasOne(d => d.Visibility).WithMany(p => p.Resources)
            .HasForeignKey(d => d.VisibilityId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_visibility");

        entity.Property(e => e.RepositoryURL)
            .HasColumnType("nvarchar(2048)")
            .HasColumnName("repository_url");

        entity.Property(e => e.AlreadyModerated)
            .HasColumnName("already_moderated");

        entity.Property(e => e.SubmitterId)
            .HasColumnType("nvarchar(450)")
            .HasColumnName("submitter_id");
        entity.HasOne(d => d.Submitter).WithMany(p => p.Resources)
            .HasForeignKey(d => d.SubmitterId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_resource_submitter");
    }
}
