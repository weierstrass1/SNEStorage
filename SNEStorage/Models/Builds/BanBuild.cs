using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class Ban
{
    public static void Build(EntityTypeBuilder<Ban> entity)
    {
        entity.ToTable("ban");

        entity.HasKey(p => p.Id)
            .HasName("PK_Ban");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.CreatedAt)
            .HasColumnName("created_at");
        entity.Property(e => e.ExpireDate)
            .HasColumnName("expire_date");
        entity.Property(e => e.IsPermanent)
            .HasColumnName("is_permanent");
        entity.Property(e => e.Reasons)
            .HasColumnType("text")
            .HasColumnName("reasons");

        entity.Property(e => e.UserId)
            .HasColumnType("nvarchar(450)")
            .HasColumnName("user_id");
        entity.HasOne(d => d.User).WithMany(p => p.Bans)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientCascade)
            .HasConstraintName("FK_ban_user");
    }
}
