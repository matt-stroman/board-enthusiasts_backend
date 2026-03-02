using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for the <see cref="TitleMediaAsset" /> entity.
/// </summary>
internal sealed class TitleMediaAssetConfiguration : IEntityTypeConfiguration<TitleMediaAsset>
{
    /// <summary>
    /// Configures the relational mapping for title media assets.
    /// </summary>
    /// <param name="builder">Entity builder for <see cref="TitleMediaAsset" />.</param>
    public void Configure(EntityTypeBuilder<TitleMediaAsset> builder)
    {
        builder.ToTable("title_media_assets", tableBuilder =>
        {
            tableBuilder.HasComment("Fixed-slot Board-style media assets for catalog titles.");
            tableBuilder.HasCheckConstraint(
                "ck_title_media_assets_media_role",
                "media_role IN ('card', 'hero', 'logo')");
            tableBuilder.HasCheckConstraint(
                "ck_title_media_assets_dimensions",
                "(width IS NULL AND height IS NULL) OR (width > 0 AND height > 0)");
        });

        builder.HasKey(mediaAsset => mediaAsset.Id)
            .HasName("pk_title_media_assets");

        builder.Property(mediaAsset => mediaAsset.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(mediaAsset => mediaAsset.TitleId)
            .HasColumnName("title_id")
            .ValueGeneratedNever();

        builder.Property(mediaAsset => mediaAsset.MediaRole)
            .HasColumnName("media_role")
            .HasMaxLength(20)
            .IsRequired()
            .HasComment("Fixed Board-style media slot such as card, hero, or logo.");

        builder.Property(mediaAsset => mediaAsset.SourceUrl)
            .HasColumnName("source_url")
            .HasMaxLength(2000)
            .IsRequired()
            .HasComment("Absolute URL for the external media asset.");

        builder.Property(mediaAsset => mediaAsset.AltText)
            .HasColumnName("alt_text")
            .HasMaxLength(500);

        builder.Property(mediaAsset => mediaAsset.MimeType)
            .HasColumnName("mime_type")
            .HasMaxLength(100);

        builder.Property(mediaAsset => mediaAsset.Width)
            .HasColumnName("width");

        builder.Property(mediaAsset => mediaAsset.Height)
            .HasColumnName("height");

        builder.Property(mediaAsset => mediaAsset.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(mediaAsset => mediaAsset.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(mediaAsset => mediaAsset.TitleId)
            .HasDatabaseName("ix_title_media_assets_title_id");

        builder.HasIndex(mediaAsset => new { mediaAsset.TitleId, mediaAsset.MediaRole })
            .IsUnique()
            .HasDatabaseName("ux_title_media_assets_title_id_media_role");

        builder.HasOne(mediaAsset => mediaAsset.Title)
            .WithMany(title => title.MediaAssets)
            .HasForeignKey(mediaAsset => mediaAsset.TitleId)
            .HasConstraintName("fk_title_media_assets_titles_title_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
