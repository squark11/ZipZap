using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Identity_EmailAndTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEmailVerified",
                schema: "identity",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "user_tokens",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_tokens_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_tokens_TokenHash",
                schema: "identity",
                table: "user_tokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_user_tokens_UserId_Type",
                schema: "identity",
                table: "user_tokens",
                columns: new[] { "UserId", "Type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_tokens",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "IsEmailVerified",
                schema: "identity",
                table: "users");
        }
    }
}
