using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZipZap.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Identity_ComplianceFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "compliance_flags",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Resolution = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_flags", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_compliance_flags_Status",
                schema: "identity",
                table: "compliance_flags",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "compliance_flags",
                schema: "identity");
        }
    }
}
