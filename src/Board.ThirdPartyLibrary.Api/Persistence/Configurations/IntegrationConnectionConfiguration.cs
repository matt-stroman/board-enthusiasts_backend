using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for studio-owned integration connections.
/// </summary>
internal sealed class IntegrationConnectionConfiguration : IEntityTypeConfiguration<IntegrationConnection>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IntegrationConnection> builder)
    {
        builder.ToTable("integration_connections", tableBuilder =>
        {
            tableBuilder.HasComment("Studio-owned reusable references to supported or custom external publishers/stores.");
            tableBuilder.HasCheckConstraint(
                "ck_integration_connections_publisher_choice",
                "(supported_publisher_id IS NOT NULL AND custom_publisher_display_name IS NULL AND custom_publisher_homepage_url IS NULL) OR " +
                "(supported_publisher_id IS NULL AND custom_publisher_display_name IS NOT NULL AND custom_publisher_homepage_url IS NOT NULL)");
        });

        builder.HasKey(connection => connection.Id)
            .HasName("pk_integration_connections");

        builder.Property(connection => connection.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(connection => connection.StudioId)
            .HasColumnName("studio_id")
            .ValueGeneratedNever();

        builder.Property(connection => connection.SupportedPublisherId)
            .HasColumnName("supported_publisher_id");

        builder.Property(connection => connection.CustomPublisherDisplayName)
            .HasColumnName("custom_publisher_display_name")
            .HasMaxLength(200);

        builder.Property(connection => connection.CustomPublisherHomepageUrl)
            .HasColumnName("custom_publisher_homepage_url")
            .HasMaxLength(2048);

        builder.Property(connection => connection.ConfigurationJson)
            .HasColumnName("config_json")
            .HasColumnType("jsonb")
            .HasComment("Provider-specific non-secret configuration values for the integration connection.");

        builder.Property(connection => connection.IsEnabled)
            .HasColumnName("is_enabled")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(connection => connection.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(connection => connection.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(connection => connection.StudioId)
            .HasDatabaseName("ix_integration_connections_studio_id");

        builder.HasIndex(connection => connection.SupportedPublisherId)
            .HasDatabaseName("ix_integration_connections_supported_publisher_id");

        builder.HasOne(connection => connection.Studio)
            .WithMany(studio => studio.IntegrationConnections)
            .HasForeignKey(connection => connection.StudioId)
            .HasConstraintName("fk_integration_connections_studios_studio_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(connection => connection.SupportedPublisher)
            .WithMany(publisher => publisher.IntegrationConnections)
            .HasForeignKey(connection => connection.SupportedPublisherId)
            .HasConstraintName("fk_integration_connections_supported_publishers_supported_publisher_id")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
