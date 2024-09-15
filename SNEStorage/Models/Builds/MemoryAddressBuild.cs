using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class MemoryAddress
{
    public static void Build(EntityTypeBuilder<MemoryAddress> entity)
    {
        entity.ToTable("memory_address");

        entity.HasKey(p => p.Id)
            .HasName("PK_Memory_Address");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.CreatedAt)
            .HasColumnName("created_at");
        entity.Property(e => e.LastUpdate)
            .HasColumnName("last_update");
        entity.Property(e => e.Size)
            .HasColumnName("size");
        entity.Property(e => e.Address)
            .HasColumnType("text")
            .HasColumnName("address");
        entity.Property(e => e.Name)
            .HasColumnType("text")
            .HasColumnName("name");
        entity.Property(e => e.Description)
            .HasColumnType("text")
            .HasColumnName("description");

        entity.Property(e => e.TypeId)
            .HasColumnName("memory_address_type_id");
        entity.HasOne(d => d.Type).WithMany(p => p.MemoryAddresses)
            .HasForeignKey(d => d.TypeId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_memory_address_memory_address_type");

        entity.Property(e => e.VideoGameId)
            .HasColumnName("videogame_id");
        entity.HasOne(d => d.Videogame).WithMany(p => p.MemoryAddresses)
            .HasForeignKey(d => d.VideoGameId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_memory_address_videogame");
    }
}
