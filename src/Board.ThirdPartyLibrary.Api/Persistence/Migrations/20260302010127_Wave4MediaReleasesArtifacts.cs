using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Board.ThirdPartyLibrary.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Wave4MediaReleasesArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "current_release_id",
                table: "titles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "title_media_assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Fixed Board-style media slot such as card, hero, or logo."),
                    source_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false, comment: "Absolute URL for the external media asset."),
                    alt_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    width = table.Column<int>(type: "integer", nullable: true),
                    height = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_title_media_assets", x => x.id);
                    table.CheckConstraint("ck_title_media_assets_dimensions", "(width IS NULL AND height IS NULL) OR (width > 0 AND height > 0)");
                    table.CheckConstraint("ck_title_media_assets_media_role", "media_role IN ('card', 'hero', 'logo')");
                    table.ForeignKey(
                        name: "fk_title_media_assets_titles_title_id",
                        column: x => x.title_id,
                        principalTable: "titles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Fixed-slot Board-style media assets for catalog titles.");

            migrationBuilder.CreateTable(
                name: "title_releases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metadata_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Public semver release identifier."),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_title_releases", x => x.id);
                    table.UniqueConstraint("ak_title_releases_id_title_id", x => new { x.id, x.title_id });
                    table.CheckConstraint("ck_title_releases_published_at", "(status = 'draft' AND published_at IS NULL) OR (status IN ('published', 'withdrawn') AND published_at IS NOT NULL)");
                    table.CheckConstraint("ck_title_releases_status", "status IN ('draft', 'published', 'withdrawn')");
                    table.ForeignKey(
                        name: "fk_title_releases_title_metadata_versions_metadata",
                        columns: x => new { x.metadata_version_id, x.title_id },
                        principalTable: "title_metadata_versions",
                        principalColumns: new[] { "id", "title_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_title_releases_titles_title_id",
                        column: x => x.title_id,
                        principalTable: "titles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Semver releases associated with catalog titles.");

            migrationBuilder.CreateTable(
                name: "release_artifacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    artifact_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    package_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    version_code = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_release_artifacts", x => x.id);
                    table.CheckConstraint("ck_release_artifacts_artifact_kind", "artifact_kind IN ('apk')");
                    table.CheckConstraint("ck_release_artifacts_file_size_bytes", "file_size_bytes IS NULL OR file_size_bytes > 0");
                    table.CheckConstraint("ck_release_artifacts_version_code", "version_code > 0");
                    table.ForeignKey(
                        name: "fk_release_artifacts_title_releases_release_id",
                        column: x => x.release_id,
                        principalTable: "title_releases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Installable artifact metadata for title releases.");

            migrationBuilder.CreateIndex(
                name: "ix_titles_current_release_id",
                table: "titles",
                column: "current_release_id");

            migrationBuilder.CreateIndex(
                name: "IX_titles_current_release_id_id",
                table: "titles",
                columns: new[] { "current_release_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_release_artifacts_release_id",
                table: "release_artifacts",
                column: "release_id");

            migrationBuilder.CreateIndex(
                name: "ux_release_artifacts_release_id_package_name_version_code",
                table: "release_artifacts",
                columns: new[] { "release_id", "package_name", "version_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_title_media_assets_title_id",
                table: "title_media_assets",
                column: "title_id");

            migrationBuilder.CreateIndex(
                name: "ux_title_media_assets_title_id_media_role",
                table: "title_media_assets",
                columns: new[] { "title_id", "media_role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_title_releases_metadata_version_id_title_id",
                table: "title_releases",
                columns: new[] { "metadata_version_id", "title_id" });

            migrationBuilder.CreateIndex(
                name: "ix_title_releases_title_id",
                table: "title_releases",
                column: "title_id");

            migrationBuilder.CreateIndex(
                name: "ux_title_releases_title_id_version",
                table: "title_releases",
                columns: new[] { "title_id", "version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_titles_title_releases_current_release",
                table: "titles",
                columns: new[] { "current_release_id", "id" },
                principalTable: "title_releases",
                principalColumns: new[] { "id", "title_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_titles_title_releases_current_release",
                table: "titles");

            migrationBuilder.DropTable(
                name: "release_artifacts");

            migrationBuilder.DropTable(
                name: "title_media_assets");

            migrationBuilder.DropTable(
                name: "title_releases");

            migrationBuilder.DropIndex(
                name: "ix_titles_current_release_id",
                table: "titles");

            migrationBuilder.DropIndex(
                name: "IX_titles_current_release_id_id",
                table: "titles");

            migrationBuilder.DropColumn(
                name: "current_release_id",
                table: "titles");
        }
    }
}
