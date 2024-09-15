using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNEStorage.Migrations
{
    /// <inheritdoc />
    public partial class update15 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "includes_gore",
                table: "resource",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "includes_politics",
                table: "resource",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "includes_porn",
                table: "resource",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "includes_sensitive_content",
                table: "resource",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "includes_slurs",
                table: "resource",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "visibility_id",
                table: "resource",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "reason",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reason", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "team",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Team", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "flag",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    reason_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flag", x => x.id);
                    table.ForeignKey(
                        name: "FK_flag_reason",
                        column: x => x.reason_id,
                        principalTable: "reason",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_flag_user",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_users",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    team_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Team_Users", x => x.id);
                    table.ForeignKey(
                        name: "FK_team_users_team",
                        column: x => x.team_id,
                        principalTable: "team",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_team_users_user",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resource_flags",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    flag_id = table.Column<long>(type: "bigint", nullable: false),
                    resource_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resource_Flags", x => x.id);
                    table.ForeignKey(
                        name: "FK_resource_flags_flag",
                        column: x => x.flag_id,
                        principalTable: "flag",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_resource_flags_resource",
                        column: x => x.resource_id,
                        principalTable: "resource",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_resource_visibility_id",
                table: "resource",
                column: "visibility_id");

            migrationBuilder.CreateIndex(
                name: "IX_flag_reason_id",
                table: "flag",
                column: "reason_id");

            migrationBuilder.CreateIndex(
                name: "IX_flag_user_id",
                table: "flag",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_resource_flags_flag_id",
                table: "resource_flags",
                column: "flag_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_resource_flags_resource_id",
                table: "resource_flags",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_users_team_id",
                table: "team_users",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_users_user_id",
                table: "team_users",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_resource_visibility",
                table: "resource",
                column: "visibility_id",
                principalTable: "visibility",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_resource_visibility",
                table: "resource");

            migrationBuilder.DropTable(
                name: "resource_flags");

            migrationBuilder.DropTable(
                name: "team_users");

            migrationBuilder.DropTable(
                name: "flag");

            migrationBuilder.DropTable(
                name: "team");

            migrationBuilder.DropTable(
                name: "reason");

            migrationBuilder.DropIndex(
                name: "IX_resource_visibility_id",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "includes_gore",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "includes_politics",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "includes_porn",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "includes_sensitive_content",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "includes_slurs",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "visibility_id",
                table: "resource");
        }
    }
}
