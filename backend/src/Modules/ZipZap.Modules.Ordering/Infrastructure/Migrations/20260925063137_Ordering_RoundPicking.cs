using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Ordering.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Ordering_RoundPicking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "order_item_pick_history",
                schema: "ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchasingRoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PickedQuantity = table.Column<int>(type: "integer", nullable: false),
                    SubstituteProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubstituteProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SubstituteUnit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    SubstituteQuantity = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ChangedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedByLabel = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_item_pick_history", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "order_item_picks",
                schema: "ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchasingRoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PickedQuantity = table.Column<int>(type: "integer", nullable: false),
                    SubstituteProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubstituteProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SubstituteUnit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    SubstituteQuantity = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByLabel = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_item_picks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_item_picks_order_items_Id",
                        column: x => x.Id,
                        principalSchema: "ordering",
                        principalTable: "order_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_item_pick_history_OrderItemId_Version",
                schema: "ordering",
                table: "order_item_pick_history",
                columns: new[] { "OrderItemId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_item_pick_history_PurchasingRoundId",
                schema: "ordering",
                table: "order_item_pick_history",
                column: "PurchasingRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_order_item_picks_PurchasingRoundId",
                schema: "ordering",
                table: "order_item_picks",
                column: "PurchasingRoundId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_item_pick_history",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "order_item_picks",
                schema: "ordering");
        }
    }
}
