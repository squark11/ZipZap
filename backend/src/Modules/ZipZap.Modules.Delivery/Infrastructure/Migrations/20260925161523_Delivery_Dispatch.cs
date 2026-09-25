using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Delivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Delivery_Dispatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedBy",
                schema: "delivery",
                table: "deliveries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DeliveryDate",
                schema: "delivery",
                table: "deliveries",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StopSequence",
                schema: "delivery",
                table: "deliveries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                schema: "delivery",
                table: "deliveries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "WindowEnd",
                schema: "delivery",
                table: "deliveries",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "WindowStart",
                schema: "delivery",
                table: "deliveries",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "delivery_history",
                schema: "delivery",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousDriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    StopSequence = table.Column<int>(type: "integer", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_history", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_DriverId_DeliveryDate_WindowStart",
                schema: "delivery",
                table: "deliveries",
                columns: new[] { "DriverId", "DeliveryDate", "WindowStart" });

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_StoreId_DeliveryDate",
                schema: "delivery",
                table: "deliveries",
                columns: new[] { "StoreId", "DeliveryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_history_DeliveryId_Version",
                schema: "delivery",
                table: "delivery_history",
                columns: new[] { "DeliveryId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "delivery_history",
                schema: "delivery");

            migrationBuilder.DropIndex(
                name: "IX_deliveries_DriverId_DeliveryDate_WindowStart",
                schema: "delivery",
                table: "deliveries");

            migrationBuilder.DropIndex(
                name: "IX_deliveries_StoreId_DeliveryDate",
                schema: "delivery",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "AssignedBy",
                schema: "delivery",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "DeliveryDate",
                schema: "delivery",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "StopSequence",
                schema: "delivery",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "delivery",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "WindowEnd",
                schema: "delivery",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "WindowStart",
                schema: "delivery",
                table: "deliveries");
        }
    }
}
