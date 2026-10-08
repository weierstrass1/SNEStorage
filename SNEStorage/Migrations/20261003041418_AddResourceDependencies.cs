using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNEStorage.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceDependencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "resource_dependency",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    resource_id = table.Column<long>(type: "bigint", nullable: false),
                    dependency_resource_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_dependency", x => x.Id);
                    table.ForeignKey(
                        name: "FK_resource_dependency_dependency_resource_id",
                        column: x => x.dependency_resource_id,
                        principalTable: "resource",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_resource_dependency_resource_id",
                        column: x => x.resource_id,
                        principalTable: "resource",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resource_external_dependency",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    resource_id = table.Column<long>(type: "bigint", nullable: false),
                    dependency_name = table.Column<string>(type: "varchar(100)", nullable: false),
                    dependency_url = table.Column<string>(type: "varchar(255)", nullable: false),
                    mirror_1 = table.Column<string>(type: "varchar(255)", nullable: true),
                    mirror_2 = table.Column<string>(type: "varchar(255)", nullable: true),
                    mirror_3 = table.Column<string>(type: "varchar(255)", nullable: true),
                    mirror_4 = table.Column<string>(type: "varchar(255)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_external_dependency", x => x.Id);
                    table.ForeignKey(
                        name: "FK_resource_external_dependency_resource_id",
                        column: x => x.resource_id,
                        principalTable: "resource",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resource_previews",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    resource_id = table.Column<long>(type: "bigint", nullable: false),
                    file_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_previews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_resource_previews_file_id",
                        column: x => x.file_id,
                        principalTable: "file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_resource_previews_resource_id",
                        column: x => x.resource_id,
                        principalTable: "resource",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_resource_dependency_dependency_resource_id",
                table: "resource_dependency",
                column: "dependency_resource_id");

            migrationBuilder.CreateIndex(
                name: "IX_resource_dependency_resource_id",
                table: "resource_dependency",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "IX_resource_external_dependency_resource_id",
                table: "resource_external_dependency",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "IX_resource_previews_file_id",
                table: "resource_previews",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "IX_resource_previews_resource_id",
                table: "resource_previews",
                column: "resource_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resource_dependency");

            migrationBuilder.DropTable(
                name: "resource_external_dependency");

            migrationBuilder.DropTable(
                name: "resource_previews");
        }
    }
}
