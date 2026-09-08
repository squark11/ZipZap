using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Delivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Delivery_DriverWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "delivery",
                table: "deliveries",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "delivery",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_DriverId",
                schema: "delivery",
                table: "deliveries",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedAtUtc",
                schema: "delivery",
                table: "outbox_messages",
                column: "ProcessedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "delivery");

            migrationBuilder.DropIndex(
                name: "IX_deliveries_DriverId",
                schema: "delivery",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "delivery",
                table: "deliveries");
        }
    }
}
