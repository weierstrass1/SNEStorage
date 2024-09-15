using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class ResourceTags
{
    public static void Build(EntityTypeBuilder<ResourceTags> entity)
    {
        entity.ToTable("resource_tags");

        entity.HasKey(p => p.Id)
            .HasName("PK_Resource_Tags");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.ResourceId)
            .HasColumnName("resource_id");
        entity.HasOne(d => d.Resource).WithMany(p => p.ResourceTags)
            .HasForeignKey(d => d.ResourceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_tags_resource");

        entity.Property(e => e.TagId)
            .HasColumnName("tag_id");
        entity.HasOne(d => d.Tag).WithMany(p => p.ResourceTags)
            .HasForeignKey(d => d.TagId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_tags_tag");
    }
}
