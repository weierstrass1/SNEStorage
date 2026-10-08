using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class ResourceExternalDependency
{
    public static void Build(EntityTypeBuilder<ResourceExternalDependency> entity)
    {
        entity.ToTable("resource_external_dependency");

        entity.HasKey(e => e.Id).HasName("PK_resource_external_dependency");

        entity.Property(e => e.ResourceId).HasColumnName("resource_id").IsRequired();
        entity.HasOne(d => d.Resource)
            .WithMany(p => p.ResourceExternalDependencies)
            .HasForeignKey(d => d.ResourceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_external_dependency_resource_id");

        entity.Property(e => e.DependencyName)
            .HasColumnType("varchar(100)")
            .HasColumnName("dependency_name")
            .IsRequired();
        entity.Property(e => e.DependencyURL)
            .HasColumnType("varchar(255)")
            .HasColumnName("dependency_url")
            .IsRequired();
        entity.Property(e => e.Mirror1)
            .HasColumnType("varchar(255)")
            .HasColumnName("mirror_1");
        entity.Property(e => e.Mirror2)
            .HasColumnType("varchar(255)")
            .HasColumnName("mirror_2");
        entity.Property(e => e.Mirror3)
            .HasColumnType("varchar(255)")
            .HasColumnName("mirror_3");
        entity.Property(e => e.Mirror4)
            .HasColumnType("varchar(255)")
            .HasColumnName("mirror_4");
    }
}
