using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ZipZap.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Catalog_StoreCoverAndPlatformCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverUrl",
                schema: "catalog",
                table: "stores",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryNote",
                schema: "catalog",
                table: "stores",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlatformCategoryKey",
                schema: "catalog",
                table: "categories",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "platform_categories",
                schema: "catalog",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_categories", x => x.Key);
                });

            migrationBuilder.InsertData(
                schema: "catalog",
                table: "platform_categories",
                columns: new[] { "Key", "IsActive", "Name", "SortOrder" },
                values: new object[,]
                {
                    { "chemia", true, "Chemia i higiena", 100 },
                    { "dziecko", true, "Dla dziecka", 110 },
                    { "mieso", true, "Mięso i wędliny", 50 },
                    { "mrozonki", true, "Mrożonki", 70 },
                    { "nabial", true, "Nabiał", 40 },
                    { "napoje", true, "Napoje", 80 },
                    { "owoce", true, "Owoce", 10 },
                    { "pieczywo", true, "Pieczywo", 30 },
                    { "ryby", true, "Ryby", 60 },
                    { "slodycze", true, "Słodycze i przekąski", 90 },
                    { "warzywa", true, "Warzywa", 20 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_categories",
                schema: "catalog");

            migrationBuilder.DropColumn(
                name: "CoverUrl",
                schema: "catalog",
                table: "stores");

            migrationBuilder.DropColumn(
                name: "DeliveryNote",
                schema: "catalog",
                table: "stores");

            migrationBuilder.DropColumn(
                name: "PlatformCategoryKey",
                schema: "catalog",
                table: "categories");
        }
    }
}
