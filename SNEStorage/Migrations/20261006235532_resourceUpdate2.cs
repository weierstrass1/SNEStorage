using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNEStorage.Migrations
{
    /// <inheritdoc />
    public partial class resourceUpdate2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_resource_AspNetUsers_ApplicationUserId",
                table: "resource");

            migrationBuilder.DropIndex(
                name: "IX_resource_ApplicationUserId",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "resource");

            migrationBuilder.AddColumn<string>(
                name: "submitter_id",
                table: "resource",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_resource_submitter_id",
                table: "resource",
                column: "submitter_id");

            migrationBuilder.AddForeignKey(
                name: "FK_resource_submitter",
                table: "resource",
                column: "submitter_id",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_resource_submitter",
                table: "resource");

            migrationBuilder.DropIndex(
                name: "IX_resource_submitter_id",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "submitter_id",
                table: "resource");

            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "resource",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_resource_ApplicationUserId",
                table: "resource",
                column: "ApplicationUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_resource_AspNetUsers_ApplicationUserId",
                table: "resource",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
