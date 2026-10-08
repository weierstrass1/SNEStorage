using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNEStorage.Migrations
{
    /// <inheritdoc />
    public partial class AddResource3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Version",
                table: "resource_version",
                newName: "version");

            migrationBuilder.RenameColumn(
                name: "Downloads",
                table: "resource_version",
                newName: "downloads");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "resource_version",
                newName: "created_at");

            migrationBuilder.AlterColumn<string>(
                name: "version",
                table: "resource_version",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "change_log",
                table: "resource_version",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "change_log",
                table: "resource_version");

            migrationBuilder.RenameColumn(
                name: "version",
                table: "resource_version",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "downloads",
                table: "resource_version",
                newName: "Downloads");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "resource_version",
                newName: "CreatedAt");

            migrationBuilder.AlterColumn<string>(
                name: "Version",
                table: "resource_version",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }
    }
}
