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
            .HasColumnType("text")
            .HasColumnName("name");
    }
}
