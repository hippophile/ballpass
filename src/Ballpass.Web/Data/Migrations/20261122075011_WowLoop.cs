using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ballpass.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class WowLoop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RepliedAt",
                table: "Interests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reply",
                table: "Interests",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReplyReadAt",
                table: "Interests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SeenAt",
                table: "Interests",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RepliedAt",
                table: "Interests");

            migrationBuilder.DropColumn(
                name: "Reply",
                table: "Interests");

            migrationBuilder.DropColumn(
                name: "ReplyReadAt",
                table: "Interests");

            migrationBuilder.DropColumn(
                name: "SeenAt",
                table: "Interests");
        }
    }
}
