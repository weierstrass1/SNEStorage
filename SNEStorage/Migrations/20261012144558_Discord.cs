using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNEStorage.Migrations
{
    /// <inheritdoc />
    public partial class Discord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "discord_linked_at",
                table: "user_info",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "discord_user_id",
                table: "user_info",
                type: "varchar(20)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "discord_username",
                table: "user_info",
                type: "nvarchar(32)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_info_discord_user_id",
                table: "user_info",
                column: "discord_user_id",
                unique: true,
                filter: "[discord_user_id] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_info_discord_user_id",
                table: "user_info");

            migrationBuilder.DropColumn(
                name: "discord_linked_at",
                table: "user_info");

            migrationBuilder.DropColumn(
                name: "discord_user_id",
                table: "user_info");

            migrationBuilder.DropColumn(
                name: "discord_username",
                table: "user_info");
        }
    }
}
