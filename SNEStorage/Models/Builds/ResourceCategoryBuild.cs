using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class ResourceCategory
{
    public static void Build(EntityTypeBuilder<ResourceCategory> entity)
    {
        entity.ToTable("resource_category");

        entity.HasKey(e => e.Id)
            .HasName("id");

        entity
            .Property(e => e.Name)
            .HasColumnName("name")
            .HasColumnType("varchar(20)");
    }
}
