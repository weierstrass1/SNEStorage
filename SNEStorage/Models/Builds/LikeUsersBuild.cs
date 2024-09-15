using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;
public partial class LikeUsers
{
    public static void Build(EntityTypeBuilder<LikeUsers> entity)
    {
        entity.ToTable("like_users");

        entity.HasKey(p => p.Id)
            .HasName("PK_like_users");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.Value)
            .HasColumnName("value");

        entity.Property(e => e.ScoreId)
            .HasColumnName("score_id");
        entity.HasOne(d => d.Score).WithMany(p => p.LikesUsers)
            .HasForeignKey(d => d.ScoreId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_like_users_score");

        entity.Property(e => e.UserId)
            .HasColumnType("nvarchar(450)")
            .HasColumnName("user_id");
        entity.HasOne(d => d.User).WithMany(p => p.LikesUsers)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_like_users_user");
    }
}
