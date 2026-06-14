using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ebook.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SaleEvent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    KiwifyOrderId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    GrossAmount = table.Column<double>(type: "REAL", nullable: false),
                    NetAmount = table.Column<double>(type: "REAL", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    UtmSource = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    UtmCampaign = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RawPayloadPath = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleEvent", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Product_KiwifyProductId",
                table: "Product",
                column: "KiwifyProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleEvent_KiwifyOrderId",
                table: "SaleEvent",
                column: "KiwifyOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleEvent_ProductId_OccurredAtUtc",
                table: "SaleEvent",
                columns: new[] { "ProductId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaleEvent");

            migrationBuilder.DropIndex(
                name: "IX_Product_KiwifyProductId",
                table: "Product");
        }
    }
}
