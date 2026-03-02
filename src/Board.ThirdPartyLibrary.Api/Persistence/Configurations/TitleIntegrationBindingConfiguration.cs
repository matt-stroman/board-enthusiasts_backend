using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for title acquisition bindings.
/// </summary>
internal sealed class TitleIntegrationBindingConfiguration : IEntityTypeConfiguration<TitleIntegrationBinding>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TitleIntegrationBinding> builder)
    {
        builder.ToTable("title_integration_bindings", tableBuilder =>
        {
            tableBuilder.HasComment("Title-scoped external acquisition bindings for supported or custom publishers/stores.");
            tableBuilder.HasCheckConstraint(
                "ck_title_integration_bindings_primary_requires_enabled",
                "NOT is_primary OR is_enabled");
        });

        builder.HasKey(binding => binding.Id)
            .HasName("pk_title_integration_bindings");

        builder.Property(binding => binding.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(binding => binding.TitleId)
            .HasColumnName("title_id")
            .ValueGeneratedNever();

        builder.Property(binding => binding.IntegrationConnectionId)
            .HasColumnName("integration_connection_id")
            .ValueGeneratedNever();

        builder.Property(binding => binding.AcquisitionUrl)
            .HasColumnName("acquisition_url")
            .HasMaxLength(2048)
            .IsRequired()
            .HasComment("Player-facing acquisition URL for the title on the external publisher/store.");

        builder.Property(binding => binding.AcquisitionLabel)
            .HasColumnName("acquisition_label")
            .HasMaxLength(200);

        builder.Property(binding => binding.ConfigurationJson)
            .HasColumnName("config_json")
            .HasColumnType("jsonb")
            .HasComment("Provider-specific non-secret configuration values for the title binding.");

        builder.Property(binding => binding.IsPrimary)
            .HasColumnName("is_primary")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(binding => binding.IsEnabled)
            .HasColumnName("is_enabled")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(binding => binding.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(binding => binding.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(binding => binding.TitleId)
            .HasDatabaseName("ix_title_integration_bindings_title_id");

        builder.HasIndex(binding => binding.IntegrationConnectionId)
            .HasDatabaseName("ix_title_integration_bindings_integration_connection_id");

        builder.HasIndex(binding => binding.TitleId)
            .IsUnique()
            .HasFilter("is_primary AND is_enabled")
            .HasDatabaseName("ux_title_integration_bindings_title_id_primary_enabled");

        builder.HasOne(binding => binding.Title)
            .WithMany(title => title.IntegrationBindings)
            .HasForeignKey(binding => binding.TitleId)
            .HasConstraintName("fk_title_integration_bindings_titles_title_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(binding => binding.IntegrationConnection)
            .WithMany(connection => connection.TitleIntegrationBindings)
            .HasForeignKey(binding => binding.IntegrationConnectionId)
            .HasConstraintName("fk_title_integration_bindings_integration_connections_integration_connection_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
