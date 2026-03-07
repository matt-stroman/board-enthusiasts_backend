using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

internal sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("user_notifications", tableBuilder =>
            tableBuilder.HasComment("Generic in-app notifications targeted to local user projections."));

        builder.HasKey(notification => notification.Id)
            .HasName("pk_user_notifications");

        builder.Property(notification => notification.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(notification => notification.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(notification => notification.Category)
            .HasColumnName("category")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(notification => notification.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(notification => notification.Body)
            .HasColumnName("body")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(notification => notification.ActionUrl)
            .HasColumnName("action_url")
            .HasMaxLength(512);

        builder.Property(notification => notification.IsRead)
            .HasColumnName("is_read")
            .IsRequired();

        builder.Property(notification => notification.ReadAtUtc)
            .HasColumnName("read_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(notification => notification.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(notification => notification.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(notification => new { notification.UserId, notification.CreatedAtUtc })
            .HasDatabaseName("ix_user_notifications_user_created_at");

        builder.HasIndex(notification => new { notification.UserId, notification.IsRead, notification.CreatedAtUtc })
            .HasDatabaseName("ix_user_notifications_user_is_read_created_at");

        builder.HasOne(notification => notification.User)
            .WithMany(user => user.Notifications)
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_notifications_users");
    }
}
