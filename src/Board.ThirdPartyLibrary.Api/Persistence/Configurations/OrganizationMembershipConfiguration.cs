using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

internal sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable("organization_memberships", tableBuilder =>
        {
            tableBuilder.HasComment("Organization-scoped memberships and roles owned by the application database.");
            tableBuilder.HasCheckConstraint(
                "ck_organization_memberships_role",
                "role IN ('owner', 'admin', 'editor')");
        });

        builder.HasKey(membership => new { membership.OrganizationId, membership.UserId })
            .HasName("pk_organization_memberships");

        builder.Property(membership => membership.OrganizationId)
            .HasColumnName("organization_id")
            .ValueGeneratedNever();

        builder.Property(membership => membership.UserId)
            .HasColumnName("user_id")
            .ValueGeneratedNever();

        builder.Property(membership => membership.Role)
            .HasColumnName("role")
            .HasMaxLength(20)
            .IsRequired()
            .HasComment("Scoped role for the user within the organization.");

        builder.Property(membership => membership.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(membership => membership.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(membership => membership.UserId)
            .HasDatabaseName("ix_organization_memberships_user_id");

        builder.HasOne(membership => membership.Organization)
            .WithMany(organization => organization.Memberships)
            .HasForeignKey(membership => membership.OrganizationId)
            .HasConstraintName("fk_organization_memberships_organizations_organization_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(membership => membership.User)
            .WithMany(user => user.OrganizationMemberships)
            .HasForeignKey(membership => membership.UserId)
            .HasConstraintName("fk_organization_memberships_users_user_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
