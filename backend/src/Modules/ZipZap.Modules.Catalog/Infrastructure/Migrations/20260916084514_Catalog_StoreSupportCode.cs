using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Catalog_StoreSupportCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupportCode",
                schema: "catalog",
                table: "stores",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_stores_SupportCode",
                schema: "catalog",
                table: "stores",
                column: "SupportCode",
                unique: true,
                filter: "\"SupportCode\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_stores_SupportCode",
                schema: "catalog",
                table: "stores");

            migrationBuilder.DropColumn(
                name: "SupportCode",
                schema: "catalog",
                table: "stores");
        }
    }
}
