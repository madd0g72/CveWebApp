using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace CveWebApp.Migrations
{
    /// <inheritdoc />
    public partial class AddKbSupersedence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KbSupersedences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    OriginalKb = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SupersedingKb = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DateAdded = table.Column<DateTime>(type: "datetime", nullable: false),
                    Product = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ProductFamily = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KbSupersedences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KbSupersedences_OriginalKb",
                table: "KbSupersedences",
                column: "OriginalKb");

            migrationBuilder.CreateIndex(
                name: "IX_KbSupersedences_OriginalKb_SupersedingKb",
                table: "KbSupersedences",
                columns: new[] { "OriginalKb", "SupersedingKb" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KbSupersedences_SupersedingKb",
                table: "KbSupersedences",
                column: "SupersedingKb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KbSupersedences");
        }
    }
}
