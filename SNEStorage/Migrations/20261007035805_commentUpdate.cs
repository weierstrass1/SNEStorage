using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNEStorage.Migrations
{
    /// <inheritdoc />
    public partial class commentUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Deleted",
                table: "comment",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "comment",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "comment_comments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    child_parent_id = table.Column<long>(type: "bigint", nullable: false),
                    child_comment_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("id", x => x.Id);
                    table.ForeignKey(
                        name: "FK_child_comment_comment",
                        column: x => x.child_comment_id,
                        principalTable: "comment",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_parent_comment_comment",
                        column: x => x.child_parent_id,
                        principalTable: "comment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comment_comments");

            migrationBuilder.DropColumn(
                name: "Deleted",
                table: "comment");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "comment");
        }
    }
}
