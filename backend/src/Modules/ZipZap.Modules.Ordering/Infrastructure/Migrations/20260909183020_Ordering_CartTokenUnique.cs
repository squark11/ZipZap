using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Ordering.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Ordering_CartTokenUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_carts_CartToken",
                schema: "ordering",
                table: "carts");

            migrationBuilder.CreateIndex(
                name: "IX_carts_CartToken",
                schema: "ordering",
                table: "carts",
                column: "CartToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_carts_CartToken",
                schema: "ordering",
                table: "carts");

            migrationBuilder.CreateIndex(
                name: "IX_carts_CartToken",
                schema: "ordering",
                table: "carts",
                column: "CartToken");
        }
    }
}
