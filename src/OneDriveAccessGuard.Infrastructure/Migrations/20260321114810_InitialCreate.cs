using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneDriveAccessGuard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExecutedBy = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    TargetItemId = table.Column<string>(type: "TEXT", nullable: false),
                    TargetItemName = table.Column<string>(type: "TEXT", nullable: false),
                    PermissionId = table.Column<string>(type: "TEXT", nullable: false),
                    BeforeState = table.Column<string>(type: "TEXT", nullable: false),
                    AfterState = table.Column<string>(type: "TEXT", nullable: false),
                    IsSuccess = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScanSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalUsersScanned = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalItemsFound = table.Column<int>(type: "INTEGER", nullable: false),
                    HighRiskCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MediumRiskCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LowRiskCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SharedItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    WebUrl = table.Column<string>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    OwnerDisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    OwnerEmail = table.Column<string>(type: "TEXT", nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsFolder = table.Column<bool>(type: "INTEGER", nullable: false),
                    RiskLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    PermissionsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserScanResults",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    RiskFiles = table.Column<int>(type: "INTEGER", nullable: false),
                    AllFiles = table.Column<int>(type: "INTEGER", nullable: false),
                    LastCheckDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserScanResults", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ExecutedAt",
                table: "AuditLogs",
                column: "ExecutedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SharedItems_OwnerId",
                table: "SharedItems",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedItems_RiskLevel",
                table: "SharedItems",
                column: "RiskLevel");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ScanSessions");

            migrationBuilder.DropTable(
                name: "SharedItems");

            migrationBuilder.DropTable(
                name: "UserScanResults");
        }
    }
}
