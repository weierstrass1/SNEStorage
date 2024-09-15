using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class MemoryAddressValues
{
    public static void Build(EntityTypeBuilder<MemoryAddressValues> entity)
    {
        entity.ToTable("memory_address_values");

        entity.HasKey(p => p.Id)
            .HasName("PK_Memory_Address_Values");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.Value)
            .HasColumnType("text")
            .HasColumnName("value");

        entity.Property(e => e.Description)
            .HasColumnType("text")
            .HasColumnName("description");

        entity.Property(e => e.MemoryAddressId)
            .HasColumnName("memory_address_id");
        entity.HasOne(d => d.MemoryAddress).WithMany(p => p.MemoryAddressValues)
            .HasForeignKey(d => d.MemoryAddressId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_memory_address_values_memory_address");
    }
}
