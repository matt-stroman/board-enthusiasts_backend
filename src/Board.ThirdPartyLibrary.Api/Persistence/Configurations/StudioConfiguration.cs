using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

internal sealed class StudioConfiguration : IEntityTypeConfiguration<Studio>
{
    public void Configure(EntityTypeBuilder<Studio> builder)
    {
        builder.ToTable("studios", tableBuilder =>
            tableBuilder.HasComment("Developer studios that own catalog content and related configuration."));

        builder.HasKey(studio => studio.Id)
            .HasName("pk_studios");

        builder.Property(studio => studio.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(studio => studio.Slug)
            .HasColumnName("slug")
            .HasMaxLength(100)
            .IsRequired()
            .HasComment("Human-readable unique route key used for public studio pages.");

        builder.HasIndex(studio => studio.Slug)
            .IsUnique()
            .HasDatabaseName("ux_studios_slug");

        builder.Property(studio => studio.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(studio => studio.Description)
            .HasColumnName("description")
            .HasMaxLength(4000);

        builder.Property(studio => studio.LogoUrl)
            .HasColumnName("logo_url")
            .HasMaxLength(2048);

        builder.Property(studio => studio.BannerUrl)
            .HasColumnName("banner_url")
            .HasMaxLength(2048);

        builder.Property(studio => studio.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(studio => studio.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasMany(studio => studio.Links)
            .WithOne(link => link.Studio)
            .HasForeignKey(link => link.StudioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
