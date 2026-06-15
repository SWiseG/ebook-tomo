using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ebook.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Optimization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OptimizationDecision",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Decision = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RationaleJson = table.Column<string>(type: "TEXT", nullable: false),
                    ActionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptimizationDecision", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OptimizationRun",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CycleNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReportPath = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptimizationRun", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationDecision_ProductId",
                table: "OptimizationDecision",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationDecision_RunId",
                table: "OptimizationDecision",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationRun_CycleNumber",
                table: "OptimizationRun",
                column: "CycleNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OptimizationDecision");

            migrationBuilder.DropTable(
                name: "OptimizationRun");
        }
    }
}
