using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Board.ThirdPartyLibrary.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Wave5SupportedPublishersAcquisition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "supported_publishers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Stable machine-friendly key for a supported publisher."),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    homepage_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supported_publishers", x => x.id);
                },
                comment: "Platform-managed canonical publisher registry entries available for developer acquisition connections.");

            migrationBuilder.CreateTable(
                name: "integration_connections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    studio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supported_publisher_id = table.Column<Guid>(type: "uuid", nullable: true),
                    custom_publisher_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    custom_publisher_homepage_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    config_json = table.Column<string>(type: "jsonb", nullable: true, comment: "Provider-specific non-secret configuration values for the integration connection."),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_integration_connections", x => x.id);
                    table.CheckConstraint("ck_integration_connections_publisher_choice", "(supported_publisher_id IS NOT NULL AND custom_publisher_display_name IS NULL AND custom_publisher_homepage_url IS NULL) OR (supported_publisher_id IS NULL AND custom_publisher_display_name IS NOT NULL AND custom_publisher_homepage_url IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_integration_connections_studios_studio_id",
                        column: x => x.studio_id,
                        principalTable: "studios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_integration_connections_supported_publishers_supported_publisher_id",
                        column: x => x.supported_publisher_id,
                        principalTable: "supported_publishers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Studio-owned reusable references to supported or custom external publishers/stores.");

            migrationBuilder.CreateTable(
                name: "title_integration_bindings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title_id = table.Column<Guid>(type: "uuid", nullable: false),
                    integration_connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    acquisition_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false, comment: "Player-facing acquisition URL for the title on the external publisher/store."),
                    acquisition_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    config_json = table.Column<string>(type: "jsonb", nullable: true, comment: "Provider-specific non-secret configuration values for the title binding."),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_title_integration_bindings", x => x.id);
                    table.CheckConstraint("ck_title_integration_bindings_primary_requires_enabled", "NOT is_primary OR is_enabled");
                    table.ForeignKey(
                        name: "fk_title_integration_bindings_integration_connections_integration_connection_id",
                        column: x => x.integration_connection_id,
                        principalTable: "integration_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_title_integration_bindings_titles_title_id",
                        column: x => x.title_id,
                        principalTable: "titles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Title-scoped external acquisition bindings for supported or custom publishers/stores.");

            migrationBuilder.InsertData(
                table: "supported_publishers",
                columns: new[] { "id", "created_at", "display_name", "homepage_url", "is_enabled", "key", "updated_at" },
                values: new object[,]
                {
                    { new Guid("12121212-1212-1212-1212-121212121212"), new DateTime(2026, 3, 2, 20, 0, 0, 0, DateTimeKind.Utc), "Game Jolt", "https://gamejolt.com/", true, "game-jolt", new DateTime(2026, 3, 2, 20, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("44444444-4444-4444-4444-444444444444"), new DateTime(2026, 3, 2, 20, 0, 0, 0, DateTimeKind.Utc), "itch.io", "https://itch.io/", true, "itch-io", new DateTime(2026, 3, 2, 20, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("55555555-5555-5555-5555-555555555555"), new DateTime(2026, 3, 2, 20, 0, 0, 0, DateTimeKind.Utc), "Humble Bundle", "https://www.humblebundle.com/", true, "humble-bundle", new DateTime(2026, 3, 2, 20, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "ix_integration_connections_studio_id",
                table: "integration_connections",
                column: "studio_id");

            migrationBuilder.CreateIndex(
                name: "ix_integration_connections_supported_publisher_id",
                table: "integration_connections",
                column: "supported_publisher_id");

            migrationBuilder.CreateIndex(
                name: "ux_supported_publishers_key",
                table: "supported_publishers",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_title_integration_bindings_integration_connection_id",
                table: "title_integration_bindings",
                column: "integration_connection_id");

            migrationBuilder.CreateIndex(
                name: "ux_title_integration_bindings_title_id_primary_enabled",
                table: "title_integration_bindings",
                column: "title_id",
                unique: true,
                filter: "is_primary AND is_enabled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "title_integration_bindings");

            migrationBuilder.DropTable(
                name: "integration_connections");

            migrationBuilder.DropTable(
                name: "supported_publishers");
        }
    }
}
