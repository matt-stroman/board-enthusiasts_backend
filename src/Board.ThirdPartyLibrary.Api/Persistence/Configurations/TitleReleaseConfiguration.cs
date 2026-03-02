using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for the <see cref="TitleRelease" /> entity.
/// </summary>
internal sealed class TitleReleaseConfiguration : IEntityTypeConfiguration<TitleRelease>
{
    /// <summary>
    /// Configures the relational mapping for title releases.
    /// </summary>
    /// <param name="builder">Entity builder for <see cref="TitleRelease" />.</param>
    public void Configure(EntityTypeBuilder<TitleRelease> builder)
    {
        builder.ToTable("title_releases", tableBuilder =>
        {
            tableBuilder.HasComment("Semver releases associated with catalog titles.");
            tableBuilder.HasCheckConstraint(
                "ck_title_releases_status",
                "status IN ('draft', 'published', 'withdrawn')");
            tableBuilder.HasCheckConstraint(
                "ck_title_releases_published_at",
                "(status = 'draft' AND published_at IS NULL) OR (status IN ('published', 'withdrawn') AND published_at IS NOT NULL)");
        });

        builder.HasKey(release => release.Id)
            .HasName("pk_title_releases");

        builder.Property(release => release.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(release => release.TitleId)
            .HasColumnName("title_id")
            .ValueGeneratedNever();

        builder.HasAlternateKey(release => new { release.Id, release.TitleId })
            .HasName("ak_title_releases_id_title_id");

        builder.Property(release => release.MetadataVersionId)
            .HasColumnName("metadata_version_id")
            .ValueGeneratedNever();

        builder.Property(release => release.Version)
            .HasColumnName("version")
            .HasMaxLength(100)
            .IsRequired()
            .HasComment("Public semver release identifier.");

        builder.Property(release => release.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(release => release.PublishedAtUtc)
            .HasColumnName("published_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(release => release.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(release => release.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(release => release.TitleId)
            .HasDatabaseName("ix_title_releases_title_id");

        builder.HasIndex(release => new { release.TitleId, release.Version })
            .IsUnique()
            .HasDatabaseName("ux_title_releases_title_id_version");

        builder.HasOne(release => release.Title)
            .WithMany(title => title.Releases)
            .HasForeignKey(release => release.TitleId)
            .HasConstraintName("fk_title_releases_titles_title_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(release => release.MetadataVersion)
            .WithMany(metadataVersion => metadataVersion.Releases)
            .HasForeignKey(release => new { release.MetadataVersionId, release.TitleId })
            .HasPrincipalKey(metadataVersion => new { metadataVersion.Id, metadataVersion.TitleId })
            .HasConstraintName("fk_title_releases_title_metadata_versions_metadata")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
