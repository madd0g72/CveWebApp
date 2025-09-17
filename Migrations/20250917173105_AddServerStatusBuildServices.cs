using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CveWebApp.Migrations
{
    /// <inheritdoc />
    public partial class AddServerStatusBuildServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Servers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VCenter = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Cluster = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Project = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Environment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IDEL = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ServerName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ServerIP = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    ManagementIP = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    OperatingSystem = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OperatingSystemVersion = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Build = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastBootTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LocalAdmins = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OSDiskSize = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OSDiskFree = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ServiceOwner = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    MaintenanceWindows = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Services = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Servers", x => x.Id);
                    table.UniqueConstraint("AK_Servers_ServerName", x => x.ServerName);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Version = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Vendor = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServerServices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServerId = table.Column<int>(type: "int", nullable: false),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    InstalledVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InstallationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsRunning = table.Column<bool>(type: "bit", nullable: false),
                    ConfigurationNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastChecked = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServerServices_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServerServices_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Servers_Environment",
                table: "Servers",
                column: "Environment");

            migrationBuilder.CreateIndex(
                name: "IX_Servers_OperatingSystem",
                table: "Servers",
                column: "OperatingSystem");

            migrationBuilder.CreateIndex(
                name: "IX_Servers_Project",
                table: "Servers",
                column: "Project");

            migrationBuilder.CreateIndex(
                name: "IX_Servers_ServerName",
                table: "Servers",
                column: "ServerName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerServices_ServerId_ServiceId",
                table: "ServerServices",
                columns: new[] { "ServerId", "ServiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerServices_ServiceId",
                table: "ServerServices",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_ServiceName",
                table: "Services",
                column: "ServiceName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ServerInstalledKbs_Servers_Computer",
                table: "ServerInstalledKbs",
                column: "Computer",
                principalTable: "Servers",
                principalColumn: "ServerName",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServerInstalledKbs_Servers_Computer",
                table: "ServerInstalledKbs");

            migrationBuilder.DropTable(
                name: "ServerServices");

            migrationBuilder.DropTable(
                name: "Servers");

            migrationBuilder.DropTable(
                name: "Services");
        }
    }
}
