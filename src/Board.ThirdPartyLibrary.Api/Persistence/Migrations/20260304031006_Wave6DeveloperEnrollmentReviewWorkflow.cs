using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Board.ThirdPartyLibrary.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Wave6DeveloperEnrollmentReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "developer_enrollment_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "Current workflow status: pending, approved, or rejected."),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_developer_enrollment_requests", x => x.id);
                    table.CheckConstraint("ck_developer_enrollment_requests_status", "status IN ('pending', 'approved', 'rejected')");
                    table.ForeignKey(
                        name: "fk_developer_enrollment_requests_reviewed_by_users",
                        column: x => x.reviewed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_developer_enrollment_requests_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Application-owned developer enrollment workflow state for player accounts.");

            migrationBuilder.CreateIndex(
                name: "IX_developer_enrollment_requests_reviewed_by_user_id",
                table: "developer_enrollment_requests",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_developer_enrollment_requests_user_id",
                table: "developer_enrollment_requests",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "developer_enrollment_requests");
        }
    }
}
