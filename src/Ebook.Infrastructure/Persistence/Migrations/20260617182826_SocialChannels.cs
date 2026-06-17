using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ebook.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SocialChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAtUtc",
                table: "SocialPost",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Channel",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    NicheId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PageId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    IgUserId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    AccessToken = table.Column<string>(type: "TEXT", maxLength: 800, nullable: true),
                    PublicMediaBaseUrl = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    TokenExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channel", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Channel_NicheId",
                table: "Channel",
                column: "NicheId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Channel");

            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                table: "SocialPost");
        }
    }
}
