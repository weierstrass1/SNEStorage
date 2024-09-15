using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class ResourceFlags
{
    public static void Build(EntityTypeBuilder<ResourceFlags> entity)
    {
        entity.ToTable("resource_flags");

        entity.HasKey(p => p.Id)
            .HasName("PK_Resource_Flags");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.ResourceId)
            .HasColumnName("resource_id");
        entity.HasOne(d => d.Resource).WithMany(p => p.ResourceFlags)
            .HasForeignKey(d => d.ResourceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_flags_resource");


        entity.Property(e => e.FlagId)
            .HasColumnName("flag_id");
        entity.HasOne(d => d.Flag).WithOne(p => p.ResourceFlags)
            .HasForeignKey<ResourceFlags>(d => d.FlagId)
            .OnDelete(DeleteBehavior.ClientCascade)
            .HasConstraintName("FK_resource_flags_flag");
    }
}
