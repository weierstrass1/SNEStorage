using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNEStorage.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueCommentLike : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_like_users_score_id",
                table: "like_users");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "visibility",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "resource_type",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_like_users_score_id_user_id",
                table: "like_users",
                columns: new[] { "score_id", "user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_like_users_score_id_user_id",
                table: "like_users");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "visibility",
                type: "nvarchar(100)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "resource_type",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_like_users_score_id",
                table: "like_users",
                column: "score_id");
        }
    }
}
