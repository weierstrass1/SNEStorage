using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class ResourceResourceTypes
{
    public static void Build(EntityTypeBuilder<ResourceResourceTypes> entity)
    {
        entity.ToTable("resource_resource_types");

        entity.HasKey(e => e.Id).HasName("PK_resource_resource_types");

        entity.Property(e => e.ResourceId)
            .HasColumnName("resource_id");
        entity.HasOne(e => e.Resource)
            .WithMany(e => e.ResourceResourceTypes)
            .HasForeignKey(e => e.ResourceId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.Property(e => e.ResourceTypeId)
            .HasColumnName("resource_type_id");
        entity.HasOne(e => e.ResourceType)
            .WithMany(e => e.ResourceResourceTypes)
            .HasForeignKey(e => e.ResourceTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
