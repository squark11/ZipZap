using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Identity_OutboxLeaseAndManualRetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastManualRetryAtUtc",
                schema: "identity",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastManualRetryByUserId",
                schema: "identity",
                table: "outbox_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedUntilUtc",
                schema: "identity",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastManualRetryAtUtc",
                schema: "identity",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "LastManualRetryByUserId",
                schema: "identity",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "LockedUntilUtc",
                schema: "identity",
                table: "outbox_messages");
        }
    }
}
