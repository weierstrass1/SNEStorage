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
            .HasColumnType("nvarchar(max)")
            .HasColumnName("name");
        entity.Property(e => e.Publisher)
            .HasColumnType("nvarchar(max)")
            .HasColumnName("publisher");

        entity.Property(e => e.ReleaseDate)
            .HasColumnName("release_date");
    }
}
