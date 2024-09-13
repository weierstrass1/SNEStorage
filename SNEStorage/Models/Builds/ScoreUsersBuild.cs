using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;
public partial class ScoreUsers
{
    public static void Build(EntityTypeBuilder<ScoreUsers> entity)
    {
        entity.ToTable("score_users");

        entity.HasKey(p => p.Id)
            .HasName("PK_Score_Users");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.Value)
            .HasColumnName("value");

        entity.Property(e => e.ScoreId)
            .HasColumnName("score_id");
        entity.HasOne(d => d.Score).WithMany(p => p.ScoresUsers)
            .HasForeignKey(d => d.ScoreId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_score_users_score");

        entity.Property(e => e.UserId)
            .HasColumnName("user_id");
        entity.HasOne(d => d.User).WithMany(p => p.ScoresUsers)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_score_users_user");
    }
}
