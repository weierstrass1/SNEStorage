using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNEStorage.Migrations
{
    /// <inheritdoc />
    public partial class update17 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ban",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    expire_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    is_permanent = table.Column<bool>(type: "bit", nullable: false),
                    reasons = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ban", x => x.id);
                    table.ForeignKey(
                        name: "FK_ban_user",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "memory_address_type",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memory_Address_Type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tag",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tag", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "memory_address",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    last_update = table.Column<DateTime>(type: "datetime2", nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    address = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    memory_address_type_id = table.Column<long>(type: "bigint", nullable: false),
                    videogame_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memory_Address", x => x.id);
                    table.ForeignKey(
                        name: "FK_memory_address_memory_address_type",
                        column: x => x.memory_address_type_id,
                        principalTable: "memory_address_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_memory_address_videogame",
                        column: x => x.videogame_id,
                        principalTable: "videogame",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resource_tags",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    resource_id = table.Column<long>(type: "bigint", nullable: false),
                    tag_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resource_Tags", x => x.id);
                    table.ForeignKey(
                        name: "FK_resource_tags_resource",
                        column: x => x.resource_id,
                        principalTable: "resource",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_resource_tags_tag",
                        column: x => x.tag_id,
                        principalTable: "tag",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "memory_address_flags",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    flag_id = table.Column<long>(type: "bigint", nullable: false),
                    memory_address_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memory_Address_Flags", x => x.id);
                    table.ForeignKey(
                        name: "FK_memory_address_flags_flag",
                        column: x => x.flag_id,
                        principalTable: "flag",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_memory_address_flags_memory_address",
                        column: x => x.memory_address_id,
                        principalTable: "memory_address",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "memory_address_tags",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    memory_address_id = table.Column<long>(type: "bigint", nullable: false),
                    tag_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memory_Address_Tags", x => x.id);
                    table.ForeignKey(
                        name: "FK_memory_address_tags_memory_address",
                        column: x => x.memory_address_id,
                        principalTable: "memory_address",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_memory_address_tags_tag",
                        column: x => x.tag_id,
                        principalTable: "tag",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "memory_address_values",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    memory_address_id = table.Column<long>(type: "bigint", nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memory_Address_Values", x => x.id);
                    table.ForeignKey(
                        name: "FK_memory_address_values_memory_address",
                        column: x => x.memory_address_id,
                        principalTable: "memory_address",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ban_user_id",
                table: "ban",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_memory_address_memory_address_type_id",
                table: "memory_address",
                column: "memory_address_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_memory_address_videogame_id",
                table: "memory_address",
                column: "videogame_id");

            migrationBuilder.CreateIndex(
                name: "IX_memory_address_flags_flag_id",
                table: "memory_address_flags",
                column: "flag_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_memory_address_flags_memory_address_id",
                table: "memory_address_flags",
                column: "memory_address_id");

            migrationBuilder.CreateIndex(
                name: "IX_memory_address_tags_memory_address_id",
                table: "memory_address_tags",
                column: "memory_address_id");

            migrationBuilder.CreateIndex(
                name: "IX_memory_address_tags_tag_id",
                table: "memory_address_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "IX_memory_address_values_memory_address_id",
                table: "memory_address_values",
                column: "memory_address_id");

            migrationBuilder.CreateIndex(
                name: "IX_resource_tags_resource_id",
                table: "resource_tags",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "IX_resource_tags_tag_id",
                table: "resource_tags",
                column: "tag_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ban");

            migrationBuilder.DropTable(
                name: "memory_address_flags");

            migrationBuilder.DropTable(
                name: "memory_address_tags");

            migrationBuilder.DropTable(
                name: "memory_address_values");

            migrationBuilder.DropTable(
                name: "resource_tags");

            migrationBuilder.DropTable(
                name: "memory_address");

            migrationBuilder.DropTable(
                name: "tag");

            migrationBuilder.DropTable(
                name: "memory_address_type");
        }
    }
}
