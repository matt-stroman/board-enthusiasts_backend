using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

internal sealed class UserPlatformRoleConfiguration : IEntityTypeConfiguration<UserPlatformRole>
{
    public void Configure(EntityTypeBuilder<UserPlatformRole> builder)
    {
        builder.ToTable("user_platform_roles", tableBuilder =>
            tableBuilder.HasComment("Local projection of platform roles observed on authenticated user claims."));

        builder.HasKey(role => new { role.UserId, role.Role })
            .HasName("pk_user_platform_roles");

        builder.Property(role => role.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(role => role.Role)
            .HasColumnName("role")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(role => role.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(role => role.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(role => role.Role)
            .HasDatabaseName("ix_user_platform_roles_role");

        builder.HasOne(role => role.User)
            .WithMany(user => user.PlatformRoles)
            .HasForeignKey(role => role.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_platform_roles_users");
    }
}
