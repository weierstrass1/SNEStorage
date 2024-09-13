using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;
public partial class FileType
{
    public static void Build(EntityTypeBuilder<FileType> entity)
    {
        entity.ToTable("file_type");

        entity.HasKey(p => p.Id)
            .HasName("PK_File_Type");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
            .HasColumnType("text")
            .HasColumnName("name");

        entity.Property(e => e.Value)
            .HasColumnType("text")
            .HasColumnName("value");
    }
}
