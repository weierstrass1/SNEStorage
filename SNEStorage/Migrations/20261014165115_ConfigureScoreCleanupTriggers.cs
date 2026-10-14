using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNEStorage.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureScoreCleanupTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_comment_score",
                table: "comment");

            migrationBuilder.DropForeignKey(
                name: "FK_resource_score",
                table: "resource");

            migrationBuilder.AddForeignKey(
                name: "FK_comment_score",
                table: "comment",
                column: "score_id",
                principalTable: "score",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_resource_score",
                table: "resource",
                column: "score_id",
                principalTable: "score",
                principalColumn: "id");

            migrationBuilder.Sql("EXEC(N'\r\n    CREATE OR ALTER TRIGGER [dbo].[TR_comment_delete_score]\r\n    ON [dbo].[comment]\r\n    AFTER DELETE\r\n    AS\r\n    BEGIN\r\n        SET NOCOUNT ON;\r\n\r\n        IF NOT EXISTS (SELECT 1 FROM deleted)\r\n            RETURN;\r\n\r\n        DELETE s\r\n        FROM [dbo].[score] AS s\r\n        INNER JOIN deleted AS d ON d.[score_id] = s.[id]\r\n        WHERE NOT EXISTS (\r\n            SELECT 1 FROM [dbo].[comment] AS c\r\n            WHERE c.[score_id] = s.[id]\r\n        )\r\n        AND NOT EXISTS (\r\n            SELECT 1 FROM [dbo].[resource] AS r\r\n            WHERE r.[score_id] = s.[id]\r\n        );\r\n    END;\r\n');");

            migrationBuilder.Sql("EXEC(N'\r\n    CREATE OR ALTER TRIGGER [dbo].[TR_resource_delete_score]\r\n    ON [dbo].[resource]\r\n    AFTER DELETE\r\n    AS\r\n    BEGIN\r\n        SET NOCOUNT ON;\r\n\r\n        IF NOT EXISTS (SELECT 1 FROM deleted)\r\n            RETURN;\r\n\r\n        DELETE s\r\n        FROM [dbo].[score] AS s\r\n        INNER JOIN deleted AS d ON d.[score_id] = s.[id]\r\n        WHERE NOT EXISTS (\r\n            SELECT 1 FROM [dbo].[comment] AS c\r\n            WHERE c.[score_id] = s.[id]\r\n        )\r\n        AND NOT EXISTS (\r\n            SELECT 1 FROM [dbo].[resource] AS r\r\n            WHERE r.[score_id] = s.[id]\r\n        );\r\n    END;\r\n');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_comment_delete_score];");

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_resource_delete_score];");

            migrationBuilder.DropForeignKey(
                name: "FK_comment_score",
                table: "comment");

            migrationBuilder.DropForeignKey(
                name: "FK_resource_score",
                table: "resource");

            migrationBuilder.AddForeignKey(
                name: "FK_comment_score",
                table: "comment",
                column: "score_id",
                principalTable: "score",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_resource_score",
                table: "resource",
                column: "score_id",
                principalTable: "score",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
