using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class ResourceMedia
{
    public static void Build(EntityTypeBuilder<ResourceMedia> entity)
    {
        entity.ToTable("resource_media");
        entity.HasKey(item => item.Id).HasName("PK_Resource_Media");
        entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        entity.Property(item => item.ResourceId).HasColumnName("resource_id");
        entity.Property(item => item.FileId).HasColumnName("file_id");
        entity.Property(item => item.PosterFileId).HasColumnName("poster_file_id");
        entity.Property(item => item.Caption).HasMaxLength(250).HasColumnName("caption");
        entity.Property(item => item.SortOrder).HasColumnName("sort_order");

        entity.HasOne(item => item.Resource)
            .WithMany(resource => resource.PreviewMedia)
            .HasForeignKey(item => item.ResourceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_media_resource");

        entity.HasOne(item => item.File)
            .WithMany(file => file.PreviewMedia)
            .HasForeignKey(item => item.FileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_media_file");

        entity.HasOne(item => item.PosterFile)
            .WithMany()
            .HasForeignKey(item => item.PosterFileId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_resource_media_poster_file");
    }
}
