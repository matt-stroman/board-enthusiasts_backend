using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

internal sealed class TitleMetadataVersionConfiguration : IEntityTypeConfiguration<TitleMetadataVersion>
{
    public void Configure(EntityTypeBuilder<TitleMetadataVersion> builder)
    {
        builder.ToTable("title_metadata_versions", tableBuilder =>
        {
            tableBuilder.HasComment("Versioned player-facing catalog metadata snapshots for titles.");
            tableBuilder.HasCheckConstraint(
                "ck_title_metadata_versions_player_counts",
                "min_players >= 1 AND max_players >= min_players");
            tableBuilder.HasCheckConstraint(
                "ck_title_metadata_versions_min_age_years",
                "min_age_years >= 0");
        });

        builder.HasKey(metadataVersion => metadataVersion.Id)
            .HasName("pk_title_metadata_versions");

        builder.Property(metadataVersion => metadataVersion.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(metadataVersion => metadataVersion.TitleId)
            .HasColumnName("title_id")
            .ValueGeneratedNever();

        builder.HasAlternateKey(metadataVersion => new { metadataVersion.Id, metadataVersion.TitleId })
            .HasName("ak_title_metadata_versions_id_title_id");

        builder.Property(metadataVersion => metadataVersion.RevisionNumber)
            .HasColumnName("revision_number")
            .IsRequired()
            .HasComment("Per-title monotonically increasing metadata revision number.");

        builder.Property(metadataVersion => metadataVersion.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(metadataVersion => metadataVersion.ShortDescription)
            .HasColumnName("short_description")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(metadataVersion => metadataVersion.Description)
            .HasColumnName("description")
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(metadataVersion => metadataVersion.GenreDisplay)
            .HasColumnName("genre_display")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(metadataVersion => metadataVersion.MinPlayers)
            .HasColumnName("min_players")
            .IsRequired();

        builder.Property(metadataVersion => metadataVersion.MaxPlayers)
            .HasColumnName("max_players")
            .IsRequired();

        builder.Property(metadataVersion => metadataVersion.AgeRatingAuthority)
            .HasColumnName("age_rating_authority")
            .HasMaxLength(50)
            .IsRequired()
            .HasComment("Age rating authority such as ESRB or PEGI.");

        builder.Property(metadataVersion => metadataVersion.AgeRatingValue)
            .HasColumnName("age_rating_value")
            .HasMaxLength(50)
            .IsRequired()
            .HasComment("Authority-specific rating value such as E10+ or 12.");

        builder.Property(metadataVersion => metadataVersion.MinAgeYears)
            .HasColumnName("min_age_years")
            .IsRequired();

        builder.Property(metadataVersion => metadataVersion.IsFrozen)
            .HasColumnName("is_frozen")
            .IsRequired()
            .HasComment("Whether the revision is immutable because the title has left draft or the revision has been preserved for history.");

        builder.Property(metadataVersion => metadataVersion.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(metadataVersion => metadataVersion.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(metadataVersion => metadataVersion.TitleId)
            .HasDatabaseName("ix_title_metadata_versions_title_id");

        builder.HasIndex(metadataVersion => new { metadataVersion.TitleId, metadataVersion.RevisionNumber })
            .IsUnique()
            .HasDatabaseName("ux_title_metadata_versions_title_id_revision_number");

        builder.HasOne(metadataVersion => metadataVersion.Title)
            .WithMany(title => title.MetadataVersions)
            .HasForeignKey(metadataVersion => metadataVersion.TitleId)
            .HasConstraintName("fk_title_metadata_versions_titles_title_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
