using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class CommentFlag
{
    public static void Build(EntityTypeBuilder<CommentFlag> entity)
    {
        entity.ToTable("comment_flags");

        entity.HasKey(p => p.Id)
            .HasName("PK_Comment_Flags");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.CommentId)
            .HasColumnName("resource_id");
        entity.HasOne(d => d.Comment).WithMany(p => p.CommentFlags)
            .HasForeignKey(d => d.CommentId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_comment_flags_comment");


        entity.Property(e => e.FlagId)
            .HasColumnName("flag_id");
        entity.HasOne(d => d.Flag).WithOne(p => p.CommentFlags)
            .HasForeignKey<CommentFlag>(d => d.FlagId)
            .OnDelete(DeleteBehavior.ClientCascade)
            .HasConstraintName("FK_comment_flags_flag");
    }
}
