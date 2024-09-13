using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;
public partial class File
{
    public static void Build(EntityTypeBuilder<File> entity)
    {
        entity.ToTable("file");

        entity.HasKey(p => p.Id)
            .HasName("PK_File");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.URL)
            .HasColumnType("text")
            .HasColumnName("url");

        entity.Property(e => e.FileTypeId)
    .       HasColumnName("file_type_id");
        entity.HasOne(d => d.FileType).WithMany(p => p.Files)
            .HasForeignKey(d => d.FileTypeId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_file_file_type");
    }
}
