using Board.ThirdPartyLibrary.Api.Identity;
using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

internal sealed class DeveloperEnrollmentRequestConfiguration : IEntityTypeConfiguration<DeveloperEnrollmentRequest>
{
    public void Configure(EntityTypeBuilder<DeveloperEnrollmentRequest> builder)
    {
        builder.ToTable("developer_enrollment_requests", tableBuilder =>
        {
            tableBuilder.HasComment("Application-owned developer enrollment workflow state for player accounts.");
            tableBuilder.HasCheckConstraint(
                "ck_developer_enrollment_requests_status",
                $"status IN ('{DeveloperEnrollmentStatuses.Pending}', '{DeveloperEnrollmentStatuses.Approved}', '{DeveloperEnrollmentStatuses.Rejected}')");
        });

        builder.HasKey(request => request.Id)
            .HasName("pk_developer_enrollment_requests");

        builder.Property(request => request.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(request => request.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.HasIndex(request => request.UserId)
            .IsUnique()
            .HasDatabaseName("ux_developer_enrollment_requests_user_id");

        builder.Property(request => request.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .IsRequired()
            .HasComment("Current workflow status: pending, approved, or rejected.");

        builder.Property(request => request.RequestedAtUtc)
            .HasColumnName("requested_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(request => request.ReviewedAtUtc)
            .HasColumnName("reviewed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(request => request.ReviewedByUserId)
            .HasColumnName("reviewed_by_user_id");

        builder.Property(request => request.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(request => request.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne(request => request.User)
            .WithOne(user => user.DeveloperEnrollmentRequest)
            .HasForeignKey<DeveloperEnrollmentRequest>(request => request.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_developer_enrollment_requests_users");

        builder.HasOne(request => request.ReviewedByUser)
            .WithMany(user => user.ReviewedDeveloperEnrollmentRequests)
            .HasForeignKey(request => request.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_developer_enrollment_requests_reviewed_by_users");
    }
}
