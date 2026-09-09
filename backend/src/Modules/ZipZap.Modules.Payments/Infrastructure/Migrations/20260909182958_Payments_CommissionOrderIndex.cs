using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Payments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Payments_CommissionOrderIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_commission_ledger_OrderId",
                schema: "payments",
                table: "commission_ledger",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_commission_ledger_OrderId",
                schema: "payments",
                table: "commission_ledger");
        }
    }
}
