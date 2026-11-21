using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ballpass.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class WowFeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Block",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BlockerKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BlockedKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Block", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Follow",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FollowerKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Follow", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Post",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuthorKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AuthorId = table.Column<int>(type: "integer", nullable: false),
                    AuthorKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PostedByCoachId = table.Column<int>(type: "integer", nullable: true),
                    Type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    Text = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: true),
                    Position = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    Region = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    AvailableFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: true),
                    EventDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Place = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Positions = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Deadline = table.Column<DateOnly>(type: "date", nullable: true),
                    Season = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Games = table.Column<int>(type: "integer", nullable: true),
                    Points = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    Rebounds = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    Assists = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    Source = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    Title = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Tag = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    BadgeKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SortAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDemo = table.Column<bool>(type: "boolean", nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Post", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kudos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    ActorKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kudos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Kudos_Post_PostId",
                        column: x => x.PostId,
                        principalTable: "Post",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostHide",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    ActorKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostHide", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostHide_Post_PostId",
                        column: x => x.PostId,
                        principalTable: "Post",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Report",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    ReporterKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Report", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Report_Post_PostId",
                        column: x => x.PostId,
                        principalTable: "Post",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Block_BlockedKey",
                table: "Block",
                column: "BlockedKey");

            migrationBuilder.CreateIndex(
                name: "IX_Block_BlockerKey_BlockedKey",
                table: "Block",
                columns: new[] { "BlockerKey", "BlockedKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Follow_FollowerKey_TargetKey",
                table: "Follow",
                columns: new[] { "FollowerKey", "TargetKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Follow_TargetKey",
                table: "Follow",
                column: "TargetKey");

            migrationBuilder.CreateIndex(
                name: "IX_Kudos_PostId_ActorKey",
                table: "Kudos",
                columns: new[] { "PostId", "ActorKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Post_AuthorKey_CreatedAt",
                table: "Post",
                columns: new[] { "AuthorKey", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Post_CreatedAt_Id",
                table: "Post",
                columns: new[] { "CreatedAt", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Post_SortAt_Id",
                table: "Post",
                columns: new[] { "SortAt", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_PostHide_ActorKey",
                table: "PostHide",
                column: "ActorKey");

            migrationBuilder.CreateIndex(
                name: "IX_PostHide_PostId_ActorKey",
                table: "PostHide",
                columns: new[] { "PostId", "ActorKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Report_PostId_ReporterKey",
                table: "Report",
                columns: new[] { "PostId", "ReporterKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Block");

            migrationBuilder.DropTable(
                name: "Follow");

            migrationBuilder.DropTable(
                name: "Kudos");

            migrationBuilder.DropTable(
                name: "PostHide");

            migrationBuilder.DropTable(
                name: "Report");

            migrationBuilder.DropTable(
                name: "Post");
        }
    }
}
