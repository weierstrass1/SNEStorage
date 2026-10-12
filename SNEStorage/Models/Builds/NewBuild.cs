using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class New
{
    public static void Build(EntityTypeBuilder<New> entity)
    {
        entity.ToTable("new");

        entity.HasKey(p => p.Id)
            .HasName("PK_New");

        entity.Property(n => n.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.CreatedAt)
            .HasColumnName("created_at");

        entity.Property(e => e.Title)
            .HasMaxLength(200)
            .IsRequired()
            .HasColumnName("title");

        entity.Property(e => e.Text)
            .HasColumnType("nvarchar(max)")
            .HasColumnName("text");

        entity.Property(n => n.PosterUserId)
            .HasColumnType("nvarchar(450)")
            .HasColumnName("poster_user_id");
        entity.HasOne(d => d.PosterUser).WithMany()
            .HasForeignKey(d => d.PosterUserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_new_poster_user");
    }
}
