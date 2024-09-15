using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace SNEStorage.Models;

public partial class MemoryAddressTags
{
    public static void Build(EntityTypeBuilder<MemoryAddressTags> entity)
    {
        entity.ToTable("memory_address_tags");

        entity.HasKey(p => p.Id)
            .HasName("PK_Memory_Address_Tags");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.MemoryAddressId)
            .HasColumnName("memory_address_id");
        entity.HasOne(d => d.MemoryAddress).WithMany(p => p.MemoryAddressTags)
            .HasForeignKey(d => d.MemoryAddressId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_memory_address_tags_memory_address");

        entity.Property(e => e.TagId)
            .HasColumnName("tag_id");
        entity.HasOne(d => d.Tag).WithMany(p => p.MemoryAddressTags)
            .HasForeignKey(d => d.TagId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_memory_address_tags_tag");
    }
}
