using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Catalog_StoreBrandingLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                schema: "catalog",
                table: "stores",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                schema: "catalog",
                table: "stores",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                schema: "catalog",
                table: "stores",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                schema: "catalog",
                table: "stores");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                schema: "catalog",
                table: "stores");

            migrationBuilder.DropColumn(
                name: "Longitude",
                schema: "catalog",
                table: "stores");
        }
    }
}
