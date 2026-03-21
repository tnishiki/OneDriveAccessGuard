using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneDriveAccessGuard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLatestToSharedItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Latest",
                table: "SharedItems",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latest",
                table: "SharedItems");
        }
    }
}
