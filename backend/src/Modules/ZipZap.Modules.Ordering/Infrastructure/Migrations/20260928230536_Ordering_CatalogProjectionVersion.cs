using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Ordering.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Ordering_CatalogProjectionVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceVersion",
                schema: "ordering",
                table: "catalog_stores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SourceVersion",
                schema: "ordering",
                table: "catalog_products",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceVersion",
                schema: "ordering",
                table: "catalog_stores");

            migrationBuilder.DropColumn(
                name: "SourceVersion",
                schema: "ordering",
                table: "catalog_products");
        }
    }
}
