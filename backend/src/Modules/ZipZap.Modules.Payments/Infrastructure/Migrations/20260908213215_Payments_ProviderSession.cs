using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Payments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Payments_ProviderSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ProviderRef",
                schema: "payments",
                table: "payments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AuthorizedAtUtc",
                schema: "payments",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFee",
                schema: "payments",
                table: "payments",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                schema: "payments",
                table: "payments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RedirectUrl",
                schema: "payments",
                table: "payments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                schema: "payments",
                table: "payments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_SessionId",
                schema: "payments",
                table: "payments",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payments_SessionId",
                schema: "payments",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "AuthorizedAtUtc",
                schema: "payments",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "DeliveryFee",
                schema: "payments",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "Provider",
                schema: "payments",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "RedirectUrl",
                schema: "payments",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "SessionId",
                schema: "payments",
                table: "payments");

            migrationBuilder.AlterColumn<string>(
                name: "ProviderRef",
                schema: "payments",
                table: "payments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);
        }
    }
}
