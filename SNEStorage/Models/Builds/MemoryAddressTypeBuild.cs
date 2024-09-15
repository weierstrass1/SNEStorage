using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class MemoryAddressType
{
    public static void Build(EntityTypeBuilder<MemoryAddressType> entity)
    {
        entity.ToTable("memory_address_type");

        entity.HasKey(p => p.Id)
            .HasName("PK_Memory_Address_Type");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
            .HasColumnType("text")
            .HasColumnName("name");
    }
}