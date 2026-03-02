using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for platform-managed supported publishers.
/// </summary>
internal sealed class SupportedPublisherConfiguration : IEntityTypeConfiguration<SupportedPublisher>
{
    /// <summary>
    /// Stable seeded identifier for the built-in itch.io publisher entry.
    /// </summary>
    public static readonly Guid ItchIoId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    /// <summary>
    /// Stable seeded identifier for the built-in Humble Bundle publisher entry.
    /// </summary>
    public static readonly Guid HumbleBundleId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    /// <summary>
    /// Stable seeded identifier for the built-in Game Jolt publisher entry.
    /// </summary>
    public static readonly Guid GameJoltId = Guid.Parse("12121212-1212-1212-1212-121212121212");

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SupportedPublisher> builder)
    {
        builder.ToTable("supported_publishers", tableBuilder =>
            tableBuilder.HasComment("Platform-managed canonical publisher registry entries available for developer acquisition connections."));

        builder.HasKey(publisher => publisher.Id)
            .HasName("pk_supported_publishers");

        builder.Property(publisher => publisher.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(publisher => publisher.Key)
            .HasColumnName("key")
            .HasMaxLength(100)
            .IsRequired()
            .HasComment("Stable machine-friendly key for a supported publisher.");

        builder.Property(publisher => publisher.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(publisher => publisher.HomepageUrl)
            .HasColumnName("homepage_url")
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(publisher => publisher.IsEnabled)
            .HasColumnName("is_enabled")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(publisher => publisher.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(publisher => publisher.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(publisher => publisher.Key)
            .IsUnique()
            .HasDatabaseName("ux_supported_publishers_key");

        builder.HasData(
            new SupportedPublisher
            {
                Id = ItchIoId,
                Key = "itch-io",
                DisplayName = "itch.io",
                HomepageUrl = "https://itch.io/",
                IsEnabled = true,
                CreatedAtUtc = DateTime.Parse("2026-03-02T20:00:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind),
                UpdatedAtUtc = DateTime.Parse("2026-03-02T20:00:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind)
            },
            new SupportedPublisher
            {
                Id = HumbleBundleId,
                Key = "humble-bundle",
                DisplayName = "Humble Bundle",
                HomepageUrl = "https://www.humblebundle.com/",
                IsEnabled = true,
                CreatedAtUtc = DateTime.Parse("2026-03-02T20:00:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind),
                UpdatedAtUtc = DateTime.Parse("2026-03-02T20:00:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind)
            },
            new SupportedPublisher
            {
                Id = GameJoltId,
                Key = "game-jolt",
                DisplayName = "Game Jolt",
                HomepageUrl = "https://gamejolt.com/",
                IsEnabled = true,
                CreatedAtUtc = DateTime.Parse("2026-03-02T20:00:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind),
                UpdatedAtUtc = DateTime.Parse("2026-03-02T20:00:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind)
            });
    }
}
