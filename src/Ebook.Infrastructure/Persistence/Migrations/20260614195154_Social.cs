using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ebook.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Social : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SocialPost",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Network = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PostType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Day = table.Column<int>(type: "INTEGER", nullable: false),
                    Caption = table.Column<string>(type: "TEXT", maxLength: 3000, nullable: false),
                    Hashtags = table.Column<string>(type: "TEXT", maxLength: 600, nullable: false),
                    ContentPath = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    MediaPath = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    Utm = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    MetricsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialPost", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SocialPost_ProductId",
                table: "SocialPost",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SocialPost_Status_ScheduledAtUtc",
                table: "SocialPost",
                columns: new[] { "Status", "ScheduledAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SocialPost");
        }
    }
}
