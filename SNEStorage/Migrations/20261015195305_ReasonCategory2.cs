using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNEStorage.Migrations
{
    /// <inheritdoc />
    public partial class ReasonCategory2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_reason_category",
                table: "reason");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ReasonCategory",
                table: "ReasonCategory");

            migrationBuilder.RenameTable(
                name: "ReasonCategory",
                newName: "reason_category");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "reason_category",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "reason_category",
                newName: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Reason_Category",
                table: "reason_category",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_reason_category_category",
                table: "reason",
                column: "reason_category_id",
                principalTable: "reason_category",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_reason_category_category",
                table: "reason");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Reason_Category",
                table: "reason_category");

            migrationBuilder.RenameTable(
                name: "reason_category",
                newName: "ReasonCategory");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "ReasonCategory",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "ReasonCategory",
                newName: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ReasonCategory",
                table: "ReasonCategory",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_reason_category",
                table: "reason",
                column: "reason_category_id",
                principalTable: "ReasonCategory",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
