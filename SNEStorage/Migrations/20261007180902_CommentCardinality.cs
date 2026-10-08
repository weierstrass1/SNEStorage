using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNEStorage.Migrations
{
    /// <inheritdoc />
    public partial class CommentCardinality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_resource_comments_comment_id",
                table: "resource_comments");

            migrationBuilder.CreateIndex(
                name: "IX_resource_comments_comment_id",
                table: "resource_comments",
                column: "comment_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_resource_comments_comment_id",
                table: "resource_comments");

            migrationBuilder.CreateIndex(
                name: "IX_resource_comments_comment_id",
                table: "resource_comments",
                column: "comment_id");
        }
    }
}
