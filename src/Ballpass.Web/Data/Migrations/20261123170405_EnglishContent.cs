using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ballpass.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnglishContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlaceEn",
                table: "Post",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PositionsEn",
                table: "Post",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextEn",
                table: "Post",
                type: "character varying(280)",
                maxLength: 280,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleEn",
                table: "Post",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BodyEn",
                table: "Message",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NoteEn",
                table: "Interests",
                type: "character varying(280)",
                maxLength: 280,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextEn",
                table: "Comment",
                type: "character varying(280)",
                maxLength: 280,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Clubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BioEn",
                table: "Athletes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlaceEn",
                table: "Post");

            migrationBuilder.DropColumn(
                name: "PositionsEn",
                table: "Post");

            migrationBuilder.DropColumn(
                name: "TextEn",
                table: "Post");

            migrationBuilder.DropColumn(
                name: "TitleEn",
                table: "Post");

            migrationBuilder.DropColumn(
                name: "BodyEn",
                table: "Message");

            migrationBuilder.DropColumn(
                name: "NoteEn",
                table: "Interests");

            migrationBuilder.DropColumn(
                name: "TextEn",
                table: "Comment");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "BioEn",
                table: "Athletes");
        }
    }
}
