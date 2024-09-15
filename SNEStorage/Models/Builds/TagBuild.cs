using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class Tag
{
    public static void Build(EntityTypeBuilder<Tag> entity)
    {
        entity.ToTable("tag");

        entity.HasKey(p => p.Id)
            .HasName("PK_Tag");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
            .HasColumnType("text")
            .HasColumnName("name");
    }
}
