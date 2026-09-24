using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Ordering.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Ordering_PurchasingRounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PurchasingRoundId",
                schema: "ordering",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RoundCutoffAtUtc",
                schema: "ordering",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RoundStartsAtUtc",
                schema: "ordering",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "purchasing_rounds",
                schema: "ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LocalTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CutoffAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchasing_rounds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "purchasing_schedule",
                schema: "ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RoundsJson = table.Column<string>(type: "text", nullable: false),
                    ActiveDaysMask = table.Column<int>(type: "integer", nullable: false),
                    MinDeliveryLeadMinutes = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchasing_schedule", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "ordering",
                table: "purchasing_schedule",
                columns: new[] { "Id", "ActiveDaysMask", "MinDeliveryLeadMinutes", "RoundsJson", "TimeZoneId", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new Guid("5c1a7e2d-0000-4000-8000-000000000001"), 126, 60, "[{\"LocalTime\":\"12:00:00\",\"CutoffMinutes\":30},{\"LocalTime\":\"16:00:00\",\"CutoffMinutes\":30}]", "Europe/Warsaw", new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Utc), null });

            migrationBuilder.CreateIndex(
                name: "IX_orders_PurchasingRoundId",
                schema: "ordering",
                table: "orders",
                column: "PurchasingRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_purchasing_rounds_StoreId_StartsAtUtc",
                schema: "ordering",
                table: "purchasing_rounds",
                columns: new[] { "StoreId", "StartsAtUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchasing_rounds",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "purchasing_schedule",
                schema: "ordering");

            migrationBuilder.DropIndex(
                name: "IX_orders_PurchasingRoundId",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PurchasingRoundId",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "RoundCutoffAtUtc",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "RoundStartsAtUtc",
                schema: "ordering",
                table: "orders");
        }
    }
}
