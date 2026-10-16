using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNEStorage.Migrations
{
    /// <inheritdoc />
    public partial class ReasonCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "reason_category_id",
                table: "reason",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "ReasonCategory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReasonCategory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reason_reason_category_id",
                table: "reason",
                column: "reason_category_id");

            migrationBuilder.AddForeignKey(
                name: "FK_reason_category",
                table: "reason",
                column: "reason_category_id",
                principalTable: "ReasonCategory",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_reason_category",
                table: "reason");

            migrationBuilder.DropTable(
                name: "ReasonCategory");

            migrationBuilder.DropIndex(
                name: "IX_reason_reason_category_id",
                table: "reason");

            migrationBuilder.DropColumn(
                name: "reason_category_id",
                table: "reason");
        }
    }
}
