using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Delivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Delivery_StatusIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_deliveries_Status",
                schema: "delivery",
                table: "deliveries",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_deliveries_Status",
                schema: "delivery",
                table: "deliveries");
        }
    }
}
