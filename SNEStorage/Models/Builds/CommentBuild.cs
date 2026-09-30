using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;
public partial class Comment
{
    public static void Build(EntityTypeBuilder<Comment> entity)
    {
        entity.ToTable("comment");

        entity.HasKey(p => p.Id)
            .HasName("PK_Comment");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.Text)
            .HasColumnType("text")
            .HasColumnName("text");

        entity.Property(e => e.PublishDate)
            .HasColumnName("publish_date");

        entity.Property(e => e.UserId)
            .HasColumnType("nvarchar(450)")
            .HasColumnName("user_id");
        entity.HasOne(d => d.User).WithMany(p => p.Comments)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_comment_user");

        entity.Property(e => e.ScoreId)
            .HasColumnName("score_id");
        entity.HasOne(d => d.Score).WithMany(p => p.Comments)
            .HasForeignKey(d => d.ScoreId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_comment_score");
    }
}
