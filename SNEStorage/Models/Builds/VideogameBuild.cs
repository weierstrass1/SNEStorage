using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;
public partial class Videogame
{
    public static void Build(EntityTypeBuilder<Videogame> entity)
    {
        entity.ToTable("videogame");

        entity.HasKey(p => p.Id)
            .HasName("PK_Videogame");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
            .HasColumnType("text")
            .HasColumnName("name");
        entity.Property(e => e.Publisher)
            .HasColumnType("text")
            .HasColumnName("publisher");

        entity.Property(e => e.ReleaseDate)
            .HasColumnName("release_date");
    }
}
