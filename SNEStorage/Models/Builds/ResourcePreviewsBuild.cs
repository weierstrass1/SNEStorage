using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class ResourcePreview
{
    public static void Build(EntityTypeBuilder<ResourcePreview> entity)
    {
        entity.ToTable("resource_previews");

        entity.HasKey(e => e.Id).HasName("PK_resource_previews");

        entity.Property(e => e.ResourceId).HasColumnName("resource_id").IsRequired();
        entity.HasOne(d => d.Resource)
            .WithMany(p => p.ResourcePreviews)
            .HasForeignKey(d => d.ResourceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_previews_resource_id");

        entity.Property(e => e.FileId).HasColumnName("file_id").IsRequired();
        entity.HasOne(d => d.File)
            .WithMany()
            .HasForeignKey(d => d.FileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_previews_file_id");
    }
}
