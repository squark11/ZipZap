using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Payments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Payments_OutboxLeaseAndManualRetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastManualRetryAtUtc",
                schema: "payments",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastManualRetryByUserId",
                schema: "payments",
                table: "outbox_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedUntilUtc",
                schema: "payments",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastManualRetryAtUtc",
                schema: "payments",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "LastManualRetryByUserId",
                schema: "payments",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "LockedUntilUtc",
                schema: "payments",
                table: "outbox_messages");
        }
    }
}
