using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class ResourceAuthors
{
    public static void Build(EntityTypeBuilder<ResourceAuthors> entity)
    {
        entity.ToTable("resource_authors");

        entity.HasKey(p => p.Id)
            .HasName("PK_Resource_Authors");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.UserId)
            .HasColumnType("nvarchar(450)")
            .HasColumnName("user_id");
        entity.HasOne(d => d.User).WithMany(p => p.ResourcesAuthors)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientCascade)
            .HasConstraintName("FK_resource_authors_user");

        entity.Property(e => e.ResourceId)
            .HasColumnName("resource_id");
        entity.HasOne(d => d.Resource).WithMany(p => p.ResourcesAuthors)
            .HasForeignKey(d => d.ResourceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_authors_resource");
    }
}
