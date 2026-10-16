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
        entity.Property(e => e.Priority)
            .HasColumnName("priority");
        entity.Property(e => e.ReasonCategoryId)
            .HasColumnName("reason_category_id");
        entity.HasOne(d => d.ReasonCategory).WithMany(e => e.Reasons)
            .HasForeignKey(d => d.ReasonCategoryId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_reason_category_category");


        entity.Property(e => e.Description)
            .HasColumnType("nvarchar(max)")
            .HasColumnName("description");
    }
}