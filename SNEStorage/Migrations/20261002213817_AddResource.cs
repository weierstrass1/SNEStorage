using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNEStorage.Migrations
{
    /// <inheritdoc />
    public partial class AddResource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_resource_file",
                table: "resource");

            migrationBuilder.DropForeignKey(
                name: "FK_resource_resource_type",
                table: "resource");

            migrationBuilder.DropForeignKey(
                name: "FK_resource_submitter_user",
                table: "resource");

            migrationBuilder.DropIndex(
                name: "IX_resource_resource_type_id",
                table: "resource");

            migrationBuilder.DropIndex(
                name: "IX_resource_submitter_user_id",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "downloads",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "last_update",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "resource_type_id",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "submitter_user_id",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "usage",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "version",
                table: "resource");

            migrationBuilder.RenameColumn(
                name: "file_id",
                table: "resource",
                newName: "FileId");

            migrationBuilder.RenameIndex(
                name: "IX_resource_file_id",
                table: "resource",
                newName: "IX_resource_FileId");

            migrationBuilder.AlterColumn<long>(
                name: "FileId",
                table: "resource",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "resource",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "resource_resource_types",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    resource_id = table.Column<long>(type: "bigint", nullable: false),
                    resource_type_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_resource_types", x => x.Id);
                    table.ForeignKey(
                        name: "FK_resource_resource_types_resource_resource_id",
                        column: x => x.resource_id,
                        principalTable: "resource",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_resource_resource_types_resource_type_resource_type_id",
                        column: x => x.resource_type_id,
                        principalTable: "resource_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResourceVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Downloads = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ResourceId = table.Column<long>(type: "bigint", nullable: false),
                    FileId = table.Column<long>(type: "bigint", nullable: false),
                    SubmitterUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceVersions_AspNetUsers_SubmitterUserId",
                        column: x => x.SubmitterUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceVersions_file_FileId",
                        column: x => x.FileId,
                        principalTable: "file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResourceVersions_resource_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "resource",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_resource_ApplicationUserId",
                table: "resource",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_resource_resource_types_resource_id",
                table: "resource_resource_types",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "IX_resource_resource_types_resource_type_id",
                table: "resource_resource_types",
                column: "resource_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceVersions_FileId",
                table: "ResourceVersions",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceVersions_ResourceId",
                table: "ResourceVersions",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceVersions_SubmitterUserId",
                table: "ResourceVersions",
                column: "SubmitterUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_resource_AspNetUsers_ApplicationUserId",
                table: "resource",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_resource_file_FileId",
                table: "resource",
                column: "FileId",
                principalTable: "file",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_resource_AspNetUsers_ApplicationUserId",
                table: "resource");

            migrationBuilder.DropForeignKey(
                name: "FK_resource_file_FileId",
                table: "resource");

            migrationBuilder.DropTable(
                name: "resource_resource_types");

            migrationBuilder.DropTable(
                name: "ResourceVersions");

            migrationBuilder.DropIndex(
                name: "IX_resource_ApplicationUserId",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "resource");

            migrationBuilder.RenameColumn(
                name: "FileId",
                table: "resource",
                newName: "file_id");

            migrationBuilder.RenameIndex(
                name: "IX_resource_FileId",
                table: "resource",
                newName: "IX_resource_file_id");

            migrationBuilder.AlterColumn<long>(
                name: "file_id",
                table: "resource",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "downloads",
                table: "resource",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_update",
                table: "resource",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<long>(
                name: "resource_type_id",
                table: "resource",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "submitter_user_id",
                table: "resource",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "usage",
                table: "resource",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "version",
                table: "resource",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_resource_resource_type_id",
                table: "resource",
                column: "resource_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_resource_submitter_user_id",
                table: "resource",
                column: "submitter_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_resource_file",
                table: "resource",
                column: "file_id",
                principalTable: "file",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_resource_resource_type",
                table: "resource",
                column: "resource_type_id",
                principalTable: "resource_type",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_resource_submitter_user",
                table: "resource",
                column: "submitter_user_id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
