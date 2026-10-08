using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;
public partial class CommentComment
{
    public static void Build(EntityTypeBuilder<CommentComment> entity)
    {
        entity.ToTable("comment_comments");

        entity.HasKey(e => e.Id).HasName("id");

        entity.Property(e => e.ParentCommentId).HasColumnName("child_parent_id");
        entity.Property(e => e.ChildCommentId).HasColumnName("child_comment_id");

        entity.HasOne(e => e.ChildComment)
            .WithOne(e => e.Parent)
            .HasForeignKey<CommentComment>(link => link.ChildCommentId)
            .OnDelete(DeleteBehavior.ClientCascade)
            .HasConstraintName("FK_child_comment_comment");

        entity.HasOne(e => e.ParentComment)
            .WithMany(e => e.Childrens)
            .HasForeignKey(link => link.ParentCommentId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_parent_comment_comment");
    }
}
