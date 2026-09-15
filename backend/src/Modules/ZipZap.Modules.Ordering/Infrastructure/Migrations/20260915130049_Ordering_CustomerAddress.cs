using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Ordering.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Ordering_CustomerAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_addresses",
                schema: "ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BuildingNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApartmentNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_addresses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_addresses_CustomerId",
                schema: "ordering",
                table: "customer_addresses",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_addresses",
                schema: "ordering");
        }
    }
}
