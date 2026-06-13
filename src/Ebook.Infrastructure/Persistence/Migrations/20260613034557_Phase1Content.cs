using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ebook.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1Content : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Artifact",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    MetaJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artifact", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeAsset",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    NicheId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Topic = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    KeywordsCsv = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ReuseCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeAsset", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Artifact_ProductId_Type_Version",
                table: "Artifact",
                columns: new[] { "ProductId", "Type", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeAsset_NicheId_Type",
                table: "KnowledgeAsset",
                columns: new[] { "NicheId", "Type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Artifact");

            migrationBuilder.DropTable(
                name: "KnowledgeAsset");
        }
    }
}
