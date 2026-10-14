using EFCore.Migrations.CustomSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class Comment
{
    public static void Build(EntityTypeBuilder<Comment> entity)
    {
        entity.ToTable("comment",
            table => table.UseSqlOutputClause(false));

        entity.HasKey(p => p.Id)
            .HasName("PK_Comment");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.Text)
            .HasColumnType("nvarchar(max)")
            .HasColumnName("text");

        entity.Property(e => e.PublishDate)
            .HasColumnName("publish_date");

        entity.Property(e => e.UserId)
            .HasColumnType("nvarchar(450)")
            .HasColumnName("user_id");
        entity.HasOne(d => d.User).WithMany(p => p.Comments)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_comment_user");

        entity.Property(e => e.ScoreId)
            .HasColumnName("score_id");
        entity.HasOne(d => d.Score).WithOne(p => p.Comment)
            .HasForeignKey<Comment>(d => d.ScoreId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_comment_score");

        entity.HasCustomSql(
            name: "TR_comment_delete_score",
            sqlUp: """
                EXEC(N'
                    CREATE OR ALTER TRIGGER [dbo].[TR_comment_delete_score]
                    ON [dbo].[comment]
                    AFTER DELETE
                    AS
                    BEGIN
                        SET NOCOUNT ON;

                        IF NOT EXISTS (SELECT 1 FROM deleted)
                            RETURN;

                        DELETE s
                        FROM [dbo].[score] AS s
                        INNER JOIN deleted AS d ON d.[score_id] = s.[id]
                        WHERE NOT EXISTS (
                            SELECT 1 FROM [dbo].[comment] AS c
                            WHERE c.[score_id] = s.[id]
                        )
                        AND NOT EXISTS (
                            SELECT 1 FROM [dbo].[resource] AS r
                            WHERE r.[score_id] = s.[id]
                        );
                    END;
                ');
                """,
            sqlDown: "DROP TRIGGER IF EXISTS [dbo].[TR_comment_delete_score];");
    }
}
