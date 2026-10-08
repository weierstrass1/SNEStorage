using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;
public partial class ResourceVersion
{
    public static void Build(EntityTypeBuilder<ResourceVersion> entity)
    {
        entity.ToTable("resource_version");

        entity.HasKey(e => e.Id).HasName("PK_resource_version");
        entity.Property(e => e.Version).HasColumnName("version").HasMaxLength(20);
        entity.Property(e => e.ChangeLog).HasColumnName("change_log").HasColumnType("nvarchar(max)");
        entity.Property(e => e.Downloads).HasColumnName("downloads").HasDefaultValue(0);
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        entity.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");
        entity.HasOne(e => e.Resource)
            .WithMany(r => r.ResourceVersions)
            .HasForeignKey(e => e.ResourceId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.File)
            .WithMany()
            .HasForeignKey(e => e.FileId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.SubmitterUser)
            .WithMany(u => u.ResourceVersions)
            .HasForeignKey(e => e.SubmitterUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.Property(e => e.AlreadyModerated)
            .HasColumnName("already_moderated");
        entity.Property(e => e.SealOfQualityRequested)
            .HasColumnName("seal_of_quality_requested");
        entity.Property(e => e.SealOfQuality)
            .HasColumnName("seal_of_quality");
    }
}
