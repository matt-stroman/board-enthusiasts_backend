using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Board.ThirdPartyLibrary.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Wave8PlayerLibraryAndTitleReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_owned_titles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_owned_titles", x => new { x.user_id, x.title_id });
                    table.ForeignKey(
                        name: "fk_player_owned_titles_titles",
                        column: x => x.title_id,
                        principalTable: "titles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_player_owned_titles_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Private owned-title library entries for players.");

            migrationBuilder.CreateTable(
                name: "player_wishlist_entries",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_wishlist_entries", x => new { x.user_id, x.title_id });
                    table.ForeignKey(
                        name: "fk_player_wishlist_entries_titles",
                        column: x => x.title_id,
                        principalTable: "titles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_player_wishlist_entries_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Private wishlist entries for players.");

            migrationBuilder.CreateTable(
                name: "title_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    resolution_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_title_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_title_reports_reporter_users",
                        column: x => x.reporter_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_title_reports_resolved_by_users",
                        column: x => x.resolved_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_title_reports_titles",
                        column: x => x.title_id,
                        principalTable: "titles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Player-submitted moderation reports for titles.");

            migrationBuilder.CreateTable(
                name: "title_report_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_title_report_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_title_report_messages_reports",
                        column: x => x.title_report_id,
                        principalTable: "title_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_title_report_messages_users",
                        column: x => x.author_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Thread messages exchanged during title-report review.");

            migrationBuilder.CreateIndex(
                name: "IX_player_owned_titles_title_id",
                table: "player_owned_titles",
                column: "title_id");

            migrationBuilder.CreateIndex(
                name: "IX_player_wishlist_entries_title_id",
                table: "player_wishlist_entries",
                column: "title_id");

            migrationBuilder.CreateIndex(
                name: "IX_title_report_messages_author_user_id",
                table: "title_report_messages",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_title_report_messages_report_created_at",
                table: "title_report_messages",
                columns: new[] { "title_report_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_title_reports_reporter_title_status",
                table: "title_reports",
                columns: new[] { "reporter_user_id", "title_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_title_reports_resolved_by_user_id",
                table: "title_reports",
                column: "resolved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_title_reports_title_status",
                table: "title_reports",
                columns: new[] { "title_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_owned_titles");

            migrationBuilder.DropTable(
                name: "player_wishlist_entries");

            migrationBuilder.DropTable(
                name: "title_report_messages");

            migrationBuilder.DropTable(
                name: "title_reports");
        }
    }
}
