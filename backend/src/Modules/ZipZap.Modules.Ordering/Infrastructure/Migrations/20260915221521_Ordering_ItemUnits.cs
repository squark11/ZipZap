using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Ordering.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Ordering_ItemUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Unit",
                schema: "ordering",
                table: "order_items",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "szt");

            migrationBuilder.AddColumn<string>(
                name: "UnitOptionsJson",
                schema: "ordering",
                table: "catalog_products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                schema: "ordering",
                table: "cart_items",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "szt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Unit",
                schema: "ordering",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "UnitOptionsJson",
                schema: "ordering",
                table: "catalog_products");

            migrationBuilder.DropColumn(
                name: "Unit",
                schema: "ordering",
                table: "cart_items");
        }
    }
}
