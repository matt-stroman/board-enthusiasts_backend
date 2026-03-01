using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Board.ThirdPartyLibrary.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Wave3TitlesMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "title_metadata_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false, comment: "Per-title monotonically increasing metadata revision number."),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    short_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    genre_display = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    min_players = table.Column<int>(type: "integer", nullable: false),
                    max_players = table.Column<int>(type: "integer", nullable: false),
                    age_rating_authority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Age rating authority such as ESRB or PEGI."),
                    age_rating_value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Authority-specific rating value such as E10+ or 12."),
                    min_age_years = table.Column<int>(type: "integer", nullable: false),
                    is_frozen = table.Column<bool>(type: "boolean", nullable: false, comment: "Whether the revision is immutable because the title has left draft or the revision has been preserved for history."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_title_metadata_versions", x => x.id);
                    table.UniqueConstraint("ak_title_metadata_versions_id_title_id", x => new { x.id, x.title_id });
                    table.CheckConstraint("ck_title_metadata_versions_min_age_years", "min_age_years >= 0");
                    table.CheckConstraint("ck_title_metadata_versions_player_counts", "min_players >= 1 AND max_players >= min_players");
                },
                comment: "Versioned player-facing catalog metadata snapshots for titles.");

            migrationBuilder.CreateTable(
                name: "titles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Human-readable unique route key scoped to the owning organization."),
                    content_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    lifecycle_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Catalog lifecycle state used to control developer and public visibility."),
                    visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Public discoverability for routes and listing behavior."),
                    current_metadata_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_titles", x => x.id);
                    table.CheckConstraint("ck_titles_content_kind", "content_kind IN ('game', 'app')");
                    table.CheckConstraint("ck_titles_lifecycle_status", "lifecycle_status IN ('draft', 'testing', 'published', 'archived')");
                    table.CheckConstraint("ck_titles_visibility", "visibility IN ('private', 'unlisted', 'listed')");
                    table.ForeignKey(
                        name: "fk_titles_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_titles_title_metadata_versions_current_metadata",
                        columns: x => new { x.current_metadata_version_id, x.id },
                        principalTable: "title_metadata_versions",
                        principalColumns: new[] { "id", "title_id" },
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Catalog titles owned by developer organizations.");

            migrationBuilder.CreateIndex(
                name: "ix_title_metadata_versions_title_id",
                table: "title_metadata_versions",
                column: "title_id");

            migrationBuilder.CreateIndex(
                name: "ux_title_metadata_versions_title_id_revision_number",
                table: "title_metadata_versions",
                columns: new[] { "title_id", "revision_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_titles_current_metadata_version_id_id",
                table: "titles",
                columns: new[] { "current_metadata_version_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_titles_organization_id",
                table: "titles",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ux_titles_organization_id_slug",
                table: "titles",
                columns: new[] { "organization_id", "slug" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_title_metadata_versions_titles_title_id",
                table: "title_metadata_versions",
                column: "title_id",
                principalTable: "titles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_title_metadata_versions_titles_title_id",
                table: "title_metadata_versions");

            migrationBuilder.DropTable(
                name: "titles");

            migrationBuilder.DropTable(
                name: "title_metadata_versions");
        }
    }
}
