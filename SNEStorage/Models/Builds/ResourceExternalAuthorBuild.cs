using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace SNEStorage.Models
{
    public partial class ResourceExternalAuthor
    {
        public static void Build(EntityTypeBuilder<ResourceExternalAuthor> entity)
        {
            entity.ToTable("resource_external_author");

            entity.HasKey(p => p.Id)
                .HasName("PK_Resource_External_Author");
            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Author)
                .HasColumnType("text")
                .HasColumnName("author");

            entity.Property(e => e.ResourceId)
                .HasColumnName("resource_id");
            entity.HasOne(d => d.Resource).WithMany(p => p.ResourceExternalAuthors)
                .HasForeignKey(d => d.ResourceId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_resource_external_author_resource");
        }
    }
}
