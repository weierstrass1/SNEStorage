using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNEStorage.Migrations
{
    /// <inheritdoc />
    public partial class FlagsAndNewsLikes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "date",
                table: "flag",
                newName: "created_at");

            migrationBuilder.AddColumn<int>(
                name: "priority",
                table: "reason",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<long>(
                name: "ScoreId",
                table: "new",
                type: "bigint",
                nullable: true);

            // Every existing news item needs its own score; 0 is not a valid FK.
            // EXEC defers column binding when generating an idempotent SQL script.
            migrationBuilder.Sql("""
                EXEC(N'
                    DECLARE @NewsScores TABLE (
                        [NewsId] bigint NOT NULL PRIMARY KEY,
                        [ScoreId] bigint NOT NULL
                    );

                    MERGE [dbo].[score] AS target
                    USING (
                        SELECT [id] AS [NewsId]
                        FROM [dbo].[new]
                        WHERE [ScoreId] IS NULL
                    ) AS source
                    ON 1 = 0
                    WHEN NOT MATCHED THEN
                        INSERT DEFAULT VALUES
                    OUTPUT source.[NewsId], inserted.[id]
                        INTO @NewsScores ([NewsId], [ScoreId]);

                    UPDATE news
                    SET news.[ScoreId] = scores.[ScoreId]
                    FROM [dbo].[new] AS news
                    INNER JOIN @NewsScores AS scores ON scores.[NewsId] = news.[id];
                ');
                """);

            migrationBuilder.AlterColumn<long>(
                name: "ScoreId",
                table: "new",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "resolved",
                table: "flag",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "resolved_at",
                table: "flag",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "comment_flags",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    flag_id = table.Column<long>(type: "bigint", nullable: false),
                    resource_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comment_Flags", x => x.id);
                    table.ForeignKey(
                        name: "FK_comment_flags_comment",
                        column: x => x.resource_id,
                        principalTable: "comment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_comment_flags_flag",
                        column: x => x.flag_id,
                        principalTable: "flag",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_new_ScoreId",
                table: "new",
                column: "ScoreId");

            migrationBuilder.CreateIndex(
                name: "IX_comment_flags_flag_id",
                table: "comment_flags",
                column: "flag_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_comment_flags_resource_id",
                table: "comment_flags",
                column: "resource_id");

            migrationBuilder.AddForeignKey(
                name: "FK_new_score_ScoreId",
                table: "new",
                column: "ScoreId",
                principalTable: "score",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_new_score_ScoreId",
                table: "new");

            migrationBuilder.DropTable(
                name: "comment_flags");

            migrationBuilder.DropIndex(
                name: "IX_new_ScoreId",
                table: "new");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "reason");

            migrationBuilder.DropColumn(
                name: "ScoreId",
                table: "new");

            migrationBuilder.DropColumn(
                name: "resolved",
                table: "flag");

            migrationBuilder.DropColumn(
                name: "resolved_at",
                table: "flag");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "flag",
                newName: "date");
        }
    }
}
