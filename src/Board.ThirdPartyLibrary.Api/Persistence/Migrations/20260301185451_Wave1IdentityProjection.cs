using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Board.ThirdPartyLibrary.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Wave1IdentityProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    keycloak_subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Immutable Keycloak subject identifier used as the durable external identity link."),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    email_verified = table.Column<bool>(type: "boolean", nullable: false),
                    identity_provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                },
                comment: "Application-owned identity projection linked to a Keycloak subject.");

            migrationBuilder.CreateTable(
                name: "user_board_profiles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    board_user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Board-owned user identifier cached for application workflows."),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    avatar_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    linked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_board_profiles", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_user_board_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Optional cached linkage between an application user and a Board profile.");

            migrationBuilder.CreateIndex(
                name: "ux_user_board_profiles_board_user_id",
                table: "user_board_profiles",
                column: "board_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_users_keycloak_subject",
                table: "users",
                column: "keycloak_subject",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_board_profiles");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
