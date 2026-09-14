using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Feedback.Migrations
{
    /// <inheritdoc />
    public partial class Feedback_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "feedback");

            migrationBuilder.CreateTable(
                name: "feedback_items",
                schema: "feedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    Screen = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AppVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Platform = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feedback_items", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_feedback_items_CreatedAtUtc",
                schema: "feedback",
                table: "feedback_items",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_feedback_items_Status",
                schema: "feedback",
                table: "feedback_items",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_feedback_items_StoreId",
                schema: "feedback",
                table: "feedback_items",
                column: "StoreId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feedback_items",
                schema: "feedback");
        }
    }
}
