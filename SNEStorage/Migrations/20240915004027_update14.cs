using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNEStorage.Migrations
{
    /// <inheritdoc />
    public partial class update14 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "time_zone",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Time_Zone", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "visibility",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Visibility", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_info",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    username_color = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "text", nullable: true),
                    avatar_id = table.Column<long>(type: "bigint", nullable: false),
                    birthday = table.Column<DateOnly>(type: "date", nullable: false),
                    bio = table.Column<string>(type: "text", nullable: true),
                    discord = table.Column<string>(type: "text", nullable: true),
                    twitter = table.Column<string>(type: "text", nullable: true),
                    facebook = table.Column<string>(type: "text", nullable: true),
                    youtube = table.Column<string>(type: "text", nullable: true),
                    instagram = table.Column<string>(type: "text", nullable: true),
                    steam = table.Column<string>(type: "text", nullable: true),
                    twitch = table.Column<string>(type: "text", nullable: true),
                    github = table.Column<string>(type: "text", nullable: true),
                    nintendo_network = table.Column<string>(type: "text", nullable: true),
                    email_visibility_id = table.Column<long>(type: "bigint", nullable: false),
                    time_zone_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User_Info", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_info_file",
                        column: x => x.avatar_id,
                        principalTable: "file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_info_time_zone",
                        column: x => x.time_zone_id,
                        principalTable: "time_zone",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_info_user",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_info_visibility",
                        column: x => x.email_visibility_id,
                        principalTable: "visibility",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_info_avatar_id",
                table: "user_info",
                column: "avatar_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_info_email_visibility_id",
                table: "user_info",
                column: "email_visibility_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_info_time_zone_id",
                table: "user_info",
                column: "time_zone_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_info_user_id",
                table: "user_info",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_info");

            migrationBuilder.DropTable(
                name: "time_zone");

            migrationBuilder.DropTable(
                name: "visibility");
        }
    }
}
