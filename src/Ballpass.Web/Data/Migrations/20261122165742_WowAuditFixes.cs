using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ballpass.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class WowAuditFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDemo",
                table: "Report",
                type: "boolean",
                nullable: false,
                defaultValue: true);   // existing rows are demo rows: the whole database is demo data

            migrationBuilder.AddColumn<bool>(
                name: "IsDemo",
                table: "Kudos",
                type: "boolean",
                nullable: false,
                defaultValue: true);   // existing rows are demo rows: the whole database is demo data

            migrationBuilder.AddColumn<bool>(
                name: "IsDemo",
                table: "Follow",
                type: "boolean",
                nullable: false,
                defaultValue: true);   // existing rows are demo rows: the whole database is demo data

            migrationBuilder.AddColumn<bool>(
                name: "IsDemo",
                table: "Block",
                type: "boolean",
                nullable: false,
                defaultValue: true);   // existing rows are demo rows: the whole database is demo data
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDemo",
                table: "Report");

            migrationBuilder.DropColumn(
                name: "IsDemo",
                table: "Kudos");

            migrationBuilder.DropColumn(
                name: "IsDemo",
                table: "Follow");

            migrationBuilder.DropColumn(
                name: "IsDemo",
                table: "Block");
        }
    }
}
