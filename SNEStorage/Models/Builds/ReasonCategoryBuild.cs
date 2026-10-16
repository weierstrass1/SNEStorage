using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class ReasonCategory
{
    public static void Build(EntityTypeBuilder<ReasonCategory> entity)
    {
        entity.ToTable("reason_category");

        entity.HasKey(p => p.Id)
            .HasName("PK_Reason_Category");

        entity.Property(n => n.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
            .IsRequired()
            .HasColumnName("name");
    }
}
