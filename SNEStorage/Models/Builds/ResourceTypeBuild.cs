using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class ResourceType
{
    public static void Build(EntityTypeBuilder<ResourceType> entity)
    {
        entity.ToTable("resource_type");

        entity.HasKey(p => p.Id)
            .HasName("PK_Resource_Type");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
            .HasColumnType("nvarchar(max)")
            .HasColumnName("name");

        entity.Property(p => p.CategoryId).HasColumnName("category_id");
        entity.HasOne(d => d.Category)
            .WithMany(p => p.Types)
            .HasForeignKey(d => d.CategoryId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_type_category");
    }
}
