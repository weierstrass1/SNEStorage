using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class Flag
{
    public static void Build(EntityTypeBuilder<Flag> entity)
    {
        entity.ToTable("flag");

        entity.HasKey(p => p.Id)
            .HasName("PK_Flag");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.UserId)
            .HasColumnType("nvarchar(450)")
            .HasColumnName("user_id");
        entity.HasOne(d => d.User).WithMany(p => p.Flags)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_flag_user");

        entity.Property(e => e.ReasonId)
            .HasColumnName("reason_id");
        entity.HasOne(d => d.Reason).WithMany(p => p.Flags)
            .HasForeignKey(d => d.ReasonId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_flag_reason");
    }
}