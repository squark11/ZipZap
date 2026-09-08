using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Ordering.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Ordering_CheckoutHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "ordering",
                table: "orders",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumOrderValue",
                schema: "ordering",
                table: "catalog_stores",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "ordering",
                table: "catalog_stores",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_orders_CustomerId_IdempotencyKey",
                schema: "ordering",
                table: "orders",
                columns: new[] { "CustomerId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_CustomerId_IdempotencyKey",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "MinimumOrderValue",
                schema: "ordering",
                table: "catalog_stores");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "ordering",
                table: "catalog_stores");
        }
    }
}
