using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class MemoryAddressFlags
{
    public static void Build(EntityTypeBuilder<MemoryAddressFlags> entity)
    {
        entity.ToTable("memory_address_flags");

        entity.HasKey(p => p.Id)
            .HasName("PK_Memory_Address_Flags");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.MemoryAddressId)
            .HasColumnName("memory_address_id");
        entity.HasOne(d => d.MemoryAddress).WithMany(p => p.MemoryAddressFlags)
            .HasForeignKey(d => d.MemoryAddressId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_memory_address_flags_memory_address");


        entity.Property(e => e.FlagId)
            .HasColumnName("flag_id");
        entity.HasOne(d => d.Flag).WithOne(p => p.MemoryAddressFlags)
            .HasForeignKey<MemoryAddressFlags>(d => d.FlagId)
            .OnDelete(DeleteBehavior.ClientCascade)
            .HasConstraintName("FK_memory_address_flags_flag");
    }
}
