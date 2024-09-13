using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;
public partial class Score
{
    public static void Build(EntityTypeBuilder<Score> entity)
    {
        entity.ToTable("score");

        entity.HasKey(p => p.Id)
            .HasName("PK_Score");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();
    }
}
