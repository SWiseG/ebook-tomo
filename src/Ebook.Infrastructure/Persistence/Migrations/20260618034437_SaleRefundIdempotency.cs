using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ebook.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SaleRefundIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SaleEvent_KiwifyOrderId",
                table: "SaleEvent");

            migrationBuilder.CreateIndex(
                name: "IX_SaleEvent_KiwifyOrderId_Type",
                table: "SaleEvent",
                columns: new[] { "KiwifyOrderId", "Type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SaleEvent_KiwifyOrderId_Type",
                table: "SaleEvent");

            migrationBuilder.CreateIndex(
                name: "IX_SaleEvent_KiwifyOrderId",
                table: "SaleEvent",
                column: "KiwifyOrderId",
                unique: true);
        }
    }
}
