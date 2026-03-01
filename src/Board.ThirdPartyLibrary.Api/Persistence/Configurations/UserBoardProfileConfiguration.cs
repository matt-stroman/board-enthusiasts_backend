using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

internal sealed class UserBoardProfileConfiguration : IEntityTypeConfiguration<UserBoardProfile>
{
    public void Configure(EntityTypeBuilder<UserBoardProfile> builder)
    {
        builder.ToTable("user_board_profiles", tableBuilder =>
            tableBuilder.HasComment("Optional cached linkage between an application user and a Board profile."));

        builder.HasKey(profile => profile.UserId)
            .HasName("pk_user_board_profiles");

        builder.Property(profile => profile.UserId)
            .HasColumnName("user_id")
            .ValueGeneratedNever();

        builder.Property(profile => profile.BoardUserId)
            .HasColumnName("board_user_id")
            .HasMaxLength(200)
            .IsRequired()
            .HasComment("Board-owned user identifier cached for application workflows.");

        builder.HasIndex(profile => profile.BoardUserId)
            .IsUnique()
            .HasDatabaseName("ux_user_board_profiles_board_user_id");

        builder.Property(profile => profile.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(profile => profile.AvatarUrl)
            .HasColumnName("avatar_url")
            .HasMaxLength(2048);

        builder.Property(profile => profile.LinkedAtUtc)
            .HasColumnName("linked_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(profile => profile.LastSyncedAtUtc)
            .HasColumnName("last_synced_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(profile => profile.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(profile => profile.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne(profile => profile.User)
            .WithOne(user => user.BoardProfile)
            .HasForeignKey<UserBoardProfile>(profile => profile.UserId)
            .HasConstraintName("fk_user_board_profiles_users_user_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
