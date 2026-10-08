using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNEStorage.Migrations
{
    /// <inheritdoc />
    public partial class resourceUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "already_moderated",
                table: "resource_version",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "seal_of_quality",
                table: "resource_version",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "seal_of_quality_requested",
                table: "resource_version",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "resource_version",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "videogame_id",
                table: "resource",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "resource",
                type: "nvarchar(50)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "resource",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "already_moderated",
                table: "resource",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "repository_url",
                table: "resource",
                type: "nvarchar(100)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "resource",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "already_moderated",
                table: "resource_version");

            migrationBuilder.DropColumn(
                name: "seal_of_quality",
                table: "resource_version");

            migrationBuilder.DropColumn(
                name: "seal_of_quality_requested",
                table: "resource_version");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "resource_version");

            migrationBuilder.DropColumn(
                name: "already_moderated",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "repository_url",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "resource");

            migrationBuilder.AlterColumn<long>(
                name: "videogame_id",
                table: "resource",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "resource",
                type: "nvarchar(50)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)");

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "resource",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
