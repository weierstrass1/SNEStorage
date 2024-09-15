using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNEStorage.Migrations
{
    /// <inheritdoc />
    public partial class update16 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "team",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "resource",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "last_update",
                table: "resource",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "date",
                table: "flag",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "resource_authors",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    resource_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resource_Authors", x => x.id);
                    table.ForeignKey(
                        name: "FK_resource_authors_resource",
                        column: x => x.resource_id,
                        principalTable: "resource",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_resource_authors_user",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "resource_external_author",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    author = table.Column<string>(type: "text", nullable: false),
                    resource_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resource_External_Author", x => x.id);
                    table.ForeignKey(
                        name: "FK_resource_external_author_resource",
                        column: x => x.resource_id,
                        principalTable: "resource",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resource_teams",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    team_id = table.Column<long>(type: "bigint", nullable: false),
                    resource_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resource_Teams", x => x.id);
                    table.ForeignKey(
                        name: "FK_resource_teams_resource",
                        column: x => x.resource_id,
                        principalTable: "resource",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_resource_teams_team",
                        column: x => x.team_id,
                        principalTable: "team",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_resource_authors_resource_id",
                table: "resource_authors",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "IX_resource_authors_user_id",
                table: "resource_authors",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_resource_external_author_resource_id",
                table: "resource_external_author",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "IX_resource_teams_resource_id",
                table: "resource_teams",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "IX_resource_teams_team_id",
                table: "resource_teams",
                column: "team_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resource_authors");

            migrationBuilder.DropTable(
                name: "resource_external_author");

            migrationBuilder.DropTable(
                name: "resource_teams");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "team");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "last_update",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "date",
                table: "flag");
        }
    }
}
