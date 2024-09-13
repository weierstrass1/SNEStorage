using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;
public partial class ResourceComments
{
    public static void Build(EntityTypeBuilder<ResourceComments> entity)
    {
        entity.ToTable("resource_comments");

        entity.HasKey(p => p.Id)
            .HasName("PK_Resource_Comments");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.ResourceId)
            .HasColumnName("resource_id");
        entity.HasOne(d => d.Resource).WithMany(p => p.ResourceComments)
            .HasForeignKey(d => d.ResourceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_comment_resource");

        entity.Property(e => e.CommentId)
            .HasColumnName("comment_id");
        entity.HasOne(d => d.Comment).WithMany(p => p.ResourceComments)
            .HasForeignKey(d => d.CommentId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_comment_comment");
    }
}
