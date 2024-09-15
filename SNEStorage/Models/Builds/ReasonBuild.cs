using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class Reason
{
    public static void Build(EntityTypeBuilder<Reason> entity)
    {
        entity.ToTable("reason");

        entity.HasKey(p => p.Id)
            .HasName("PK_Reason");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.Description)
            .HasColumnType("text")
            .HasColumnName("description");
    }
}