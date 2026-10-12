using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNEStorage.Migrations
{
    /// <inheritdoc />
    public partial class FixLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_score_users_score_id",
                table: "score_users");

            migrationBuilder.DropIndex(
                name: "IX_resource_score_id",
                table: "resource");

            migrationBuilder.DropIndex(
                name: "IX_comment_score_id",
                table: "comment");

            migrationBuilder.AlterColumn<string>(
                name: "version",
                table: "resource_version",
                type: "nvarchar(25)",
                maxLength: 25,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "change_log",
                table: "resource_version",
                type: "nvarchar(2048)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "repository_url",
                table: "resource",
                type: "nvarchar(2048)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "resource",
                type: "nvarchar(100)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)");

            migrationBuilder.CreateIndex(
                name: "IX_score_users_score_id_user_id",
                table: "score_users",
                columns: new[] { "score_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_resource_score_id",
                table: "resource",
                column: "score_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_comment_score_id",
                table: "comment",
                column: "score_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_score_users_score_id_user_id",
                table: "score_users");

            migrationBuilder.DropIndex(
                name: "IX_resource_score_id",
                table: "resource");

            migrationBuilder.DropIndex(
                name: "IX_comment_score_id",
                table: "comment");

            migrationBuilder.AlterColumn<string>(
                name: "version",
                table: "resource_version",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(25)",
                oldMaxLength: 25,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "change_log",
                table: "resource_version",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "repository_url",
                table: "resource",
                type: "nvarchar(100)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "resource",
                type: "nvarchar(50)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)");

            migrationBuilder.CreateIndex(
                name: "IX_score_users_score_id",
                table: "score_users",
                column: "score_id");

            migrationBuilder.CreateIndex(
                name: "IX_resource_score_id",
                table: "resource",
                column: "score_id");

            migrationBuilder.CreateIndex(
                name: "IX_comment_score_id",
                table: "comment",
                column: "score_id");
        }
    }
}
