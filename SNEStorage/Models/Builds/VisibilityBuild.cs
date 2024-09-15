using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class Visibility
{
    public static void Build(EntityTypeBuilder<Visibility> entity)
    {
        entity.ToTable("visibility");

        entity.HasKey(p => p.Id)
            .HasName("PK_Visibility");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
            .HasColumnType("text")
            .HasColumnName("name");
    }
}
