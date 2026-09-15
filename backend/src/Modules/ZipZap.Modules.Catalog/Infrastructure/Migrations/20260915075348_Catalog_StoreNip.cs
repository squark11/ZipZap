using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Catalog_StoreNip : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Nip",
                schema: "catalog",
                table: "stores",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Nip",
                schema: "catalog",
                table: "stores");
        }
    }
}
