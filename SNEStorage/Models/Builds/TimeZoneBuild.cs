using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class TimeZone
{
    public static void Build(EntityTypeBuilder<TimeZone> entity)
    {
        entity.ToTable("time_zone");

        entity.HasKey(p => p.Id)
            .HasName("PK_Time_Zone");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
            .HasColumnType("text")
            .HasColumnName("name");

        entity.Property(e => e.Value)
            .HasColumnName("value");
    }
}
