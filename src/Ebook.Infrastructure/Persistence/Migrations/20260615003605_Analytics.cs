using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ebook.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Analytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalyticsEvent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UtmSource = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    UtmCampaign = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    UtmContent = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsEvent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetricDaily",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DateUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Visits = table.Column<int>(type: "INTEGER", nullable: false),
                    CheckoutClicks = table.Column<int>(type: "INTEGER", nullable: false),
                    Sales = table.Column<int>(type: "INTEGER", nullable: false),
                    Revenue = table.Column<double>(type: "REAL", nullable: false),
                    ConversionRate = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricDaily", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEvent_OccurredAtUtc",
                table: "AnalyticsEvent",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEvent_ProductId_OccurredAtUtc",
                table: "AnalyticsEvent",
                columns: new[] { "ProductId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MetricDaily_DateUtc",
                table: "MetricDaily",
                column: "DateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MetricDaily_ProductId_DateUtc_Channel",
                table: "MetricDaily",
                columns: new[] { "ProductId", "DateUtc", "Channel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalyticsEvent");

            migrationBuilder.DropTable(
                name: "MetricDaily");
        }
    }
}
