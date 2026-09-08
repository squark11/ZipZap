using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Payments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Payments_CustomerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "payments",
                table: "payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_payments_CustomerId",
                schema: "payments",
                table: "payments",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payments_CustomerId",
                schema: "payments",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "payments",
                table: "payments");
        }
    }
}
