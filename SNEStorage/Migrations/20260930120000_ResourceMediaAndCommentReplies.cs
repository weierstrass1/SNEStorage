using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SNEStorage.Models;

#nullable disable

namespace SNEStorage.Migrations;

[DbContext(typeof(SnestorageContext))]
[Migration("20260930120000_ResourceMediaAndCommentReplies")]
public partial class ResourceMediaAndCommentReplies : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "parent_comment_id",
            table: "comment",
            type: "bigint",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "resource_media",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                resource_id = table.Column<long>(type: "bigint", nullable: false),
                file_id = table.Column<long>(type: "bigint", nullable: false),
                poster_file_id = table.Column<long>(type: "bigint", nullable: true),
                caption = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                sort_order = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Resource_Media", x => x.id);
                table.ForeignKey(
                    name: "FK_resource_media_file",
                    column: x => x.file_id,
                    principalTable: "file",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_resource_media_poster_file",
                    column: x => x.poster_file_id,
                    principalTable: "file",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_resource_media_resource",
                    column: x => x.resource_id,
                    principalTable: "resource",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_comment_parent_comment_id",
            table: "comment",
            column: "parent_comment_id");

        migrationBuilder.CreateIndex(
            name: "IX_resource_media_file_id",
            table: "resource_media",
            column: "file_id");

        migrationBuilder.CreateIndex(
            name: "IX_resource_media_poster_file_id",
            table: "resource_media",
            column: "poster_file_id");

        migrationBuilder.CreateIndex(
            name: "IX_resource_media_resource_id",
            table: "resource_media",
            column: "resource_id");

        migrationBuilder.AddForeignKey(
            name: "FK_comment_parent_comment",
            table: "comment",
            column: "parent_comment_id",
            principalTable: "comment",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_comment_parent_comment", table: "comment");
        migrationBuilder.DropTable(name: "resource_media");
        migrationBuilder.DropIndex(name: "IX_comment_parent_comment_id", table: "comment");
        migrationBuilder.DropColumn(name: "parent_comment_id", table: "comment");
    }
}
