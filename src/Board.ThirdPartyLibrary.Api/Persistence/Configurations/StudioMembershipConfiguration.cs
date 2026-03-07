using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

internal sealed class StudioMembershipConfiguration : IEntityTypeConfiguration<StudioMembership>
{
    public void Configure(EntityTypeBuilder<StudioMembership> builder)
    {
        builder.ToTable("studio_memberships", tableBuilder =>
        {
            tableBuilder.HasComment("Studio-scoped memberships and roles owned by the application database.");
            tableBuilder.HasCheckConstraint(
                "ck_studio_memberships_role",
                "role IN ('owner', 'admin', 'editor')");
        });

        builder.HasKey(membership => new { membership.StudioId, membership.UserId })
            .HasName("pk_studio_memberships");

        builder.Property(membership => membership.StudioId)
            .HasColumnName("studio_id")
            .ValueGeneratedNever();

        builder.Property(membership => membership.UserId)
            .HasColumnName("user_id")
            .ValueGeneratedNever();

        builder.Property(membership => membership.Role)
            .HasColumnName("role")
            .HasMaxLength(20)
            .IsRequired()
            .HasComment("Scoped role for the user within the studio.");

        builder.Property(membership => membership.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(membership => membership.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(membership => membership.UserId)
            .HasDatabaseName("ix_studio_memberships_user_id");

        builder.HasOne(membership => membership.Studio)
            .WithMany(studio => studio.Memberships)
            .HasForeignKey(membership => membership.StudioId)
            .HasConstraintName("fk_studio_memberships_studios_studio_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(membership => membership.User)
            .WithMany(user => user.StudioMemberships)
            .HasForeignKey(membership => membership.UserId)
            .HasConstraintName("fk_studio_memberships_users_user_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
