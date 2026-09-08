using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Catalog_StoreRichness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MinimumOrderValue",
                schema: "catalog",
                table: "stores",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                schema: "catalog",
                table: "stores",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "catalog",
                table: "stores",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinimumOrderValue",
                schema: "catalog",
                table: "stores");

            migrationBuilder.DropColumn(
                name: "Phone",
                schema: "catalog",
                table: "stores");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "catalog",
                table: "stores");
        }
    }
}
