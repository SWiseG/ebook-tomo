using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ebook.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLpVariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VariantTag",
                table: "AnalyticsEvent",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LpVariant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VariantTag = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LpVariant", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEvent_ProductId_VariantTag_OccurredAtUtc",
                table: "AnalyticsEvent",
                columns: new[] { "ProductId", "VariantTag", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LpVariant_ProductId_VariantTag",
                table: "LpVariant",
                columns: new[] { "ProductId", "VariantTag" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LpVariant");

            migrationBuilder.DropIndex(
                name: "IX_AnalyticsEvent_ProductId_VariantTag_OccurredAtUtc",
                table: "AnalyticsEvent");

            migrationBuilder.DropColumn(
                name: "VariantTag",
                table: "AnalyticsEvent");
        }
    }
}
