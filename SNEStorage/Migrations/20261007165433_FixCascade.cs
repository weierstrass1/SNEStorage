using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNEStorage.Migrations
{
    /// <inheritdoc />
    public partial class FixCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_comment_comments_child_comment_id",
                table: "comment_comments");

            migrationBuilder.DropIndex(
                name: "IX_comment_comments_child_parent_id",
                table: "comment_comments");

            migrationBuilder.CreateIndex(
                name: "IX_comment_comments_child_comment_id",
                table: "comment_comments",
                column: "child_comment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_comment_comments_child_parent_id",
                table: "comment_comments",
                column: "child_parent_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_comment_comments_child_comment_id",
                table: "comment_comments");

            migrationBuilder.DropIndex(
                name: "IX_comment_comments_child_parent_id",
                table: "comment_comments");

            migrationBuilder.CreateIndex(
                name: "IX_comment_comments_child_comment_id",
                table: "comment_comments",
                column: "child_comment_id");

            migrationBuilder.CreateIndex(
                name: "IX_comment_comments_child_parent_id",
                table: "comment_comments",
                column: "child_parent_id",
                unique: true);
        }
    }
}
