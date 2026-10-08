using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNEStorage.Migrations
{
    /// <inheritdoc />
    public partial class AddResource2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ResourceVersions_AspNetUsers_SubmitterUserId",
                table: "ResourceVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceVersions_file_FileId",
                table: "ResourceVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceVersions_resource_ResourceId",
                table: "ResourceVersions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ResourceVersions",
                table: "ResourceVersions");

            migrationBuilder.RenameTable(
                name: "ResourceVersions",
                newName: "resource_version");

            migrationBuilder.RenameIndex(
                name: "IX_ResourceVersions_SubmitterUserId",
                table: "resource_version",
                newName: "IX_resource_version_SubmitterUserId");

            migrationBuilder.RenameIndex(
                name: "IX_ResourceVersions_ResourceId",
                table: "resource_version",
                newName: "IX_resource_version_ResourceId");

            migrationBuilder.RenameIndex(
                name: "IX_ResourceVersions_FileId",
                table: "resource_version",
                newName: "IX_resource_version_FileId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_resource_version",
                table: "resource_version",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_resource_version_AspNetUsers_SubmitterUserId",
                table: "resource_version",
                column: "SubmitterUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_resource_version_file_FileId",
                table: "resource_version",
                column: "FileId",
                principalTable: "file",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_resource_version_resource_ResourceId",
                table: "resource_version",
                column: "ResourceId",
                principalTable: "resource",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_resource_version_AspNetUsers_SubmitterUserId",
                table: "resource_version");

            migrationBuilder.DropForeignKey(
                name: "FK_resource_version_file_FileId",
                table: "resource_version");

            migrationBuilder.DropForeignKey(
                name: "FK_resource_version_resource_ResourceId",
                table: "resource_version");

            migrationBuilder.DropPrimaryKey(
                name: "PK_resource_version",
                table: "resource_version");

            migrationBuilder.RenameTable(
                name: "resource_version",
                newName: "ResourceVersions");

            migrationBuilder.RenameIndex(
                name: "IX_resource_version_SubmitterUserId",
                table: "ResourceVersions",
                newName: "IX_ResourceVersions_SubmitterUserId");

            migrationBuilder.RenameIndex(
                name: "IX_resource_version_ResourceId",
                table: "ResourceVersions",
                newName: "IX_ResourceVersions_ResourceId");

            migrationBuilder.RenameIndex(
                name: "IX_resource_version_FileId",
                table: "ResourceVersions",
                newName: "IX_ResourceVersions_FileId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ResourceVersions",
                table: "ResourceVersions",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceVersions_AspNetUsers_SubmitterUserId",
                table: "ResourceVersions",
                column: "SubmitterUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceVersions_file_FileId",
                table: "ResourceVersions",
                column: "FileId",
                principalTable: "file",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceVersions_resource_ResourceId",
                table: "ResourceVersions",
                column: "ResourceId",
                principalTable: "resource",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
