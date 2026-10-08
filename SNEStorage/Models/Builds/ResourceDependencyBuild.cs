using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class ResourceDependency
{
    public static void Build(EntityTypeBuilder<ResourceDependency> entity)
    {
        entity.ToTable("resource_dependency");

        entity.HasKey(e => e.Id).HasName("PK_resource_dependency");

        entity.Property(e => e.ResourceId).HasColumnName("resource_id").IsRequired();
        entity.HasOne(d => d.Resource)
            .WithMany(p => p.ResourceDependencies)
            .HasForeignKey(d => d.ResourceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_dependency_resource_id");

        entity.Property(e => e.DependencyResourceId).HasColumnName("dependency_resource_id").IsRequired();
        entity.HasOne(d => d.DependencyResource)
            .WithMany(p => p.Dependents)
            .HasForeignKey(d => d.DependencyResourceId)
            .OnDelete(DeleteBehavior.ClientCascade)
            .HasConstraintName("FK_resource_dependency_dependency_resource_id");
    }
}
