using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ebook.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrendSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrendSnapshot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    NicheId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PayloadPath = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    CollectedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrendSnapshot", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrendSnapshot_NicheId_CollectedAtUtc",
                table: "TrendSnapshot",
                columns: new[] { "NicheId", "CollectedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrendSnapshot");
        }
    }
}
