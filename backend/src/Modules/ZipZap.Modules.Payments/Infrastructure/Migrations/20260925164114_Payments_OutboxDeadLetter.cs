using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Payments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Payments_OutboxDeadLetter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeadLetteredAtUtc",
                schema: "payments",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAtUtc",
                schema: "payments",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeadLetteredAtUtc",
                schema: "payments",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "NextAttemptAtUtc",
                schema: "payments",
                table: "outbox_messages");
        }
    }
}
