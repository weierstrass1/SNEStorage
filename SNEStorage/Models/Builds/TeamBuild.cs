using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class Team
{
    public static void Build(EntityTypeBuilder<Team> entity)
    {
        entity.ToTable("team");

        entity.HasKey(p => p.Id)
            .HasName("PK_Team");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
            .HasColumnType("text")
            .HasColumnName("name");
    }
}
