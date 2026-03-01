using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

internal sealed class TitleConfiguration : IEntityTypeConfiguration<Title>
{
    public void Configure(EntityTypeBuilder<Title> builder)
    {
        builder.ToTable("titles", tableBuilder =>
        {
            tableBuilder.HasComment("Catalog titles owned by developer organizations.");
            tableBuilder.HasCheckConstraint(
                "ck_titles_content_kind",
                "content_kind IN ('game', 'app')");
            tableBuilder.HasCheckConstraint(
                "ck_titles_lifecycle_status",
                "lifecycle_status IN ('draft', 'testing', 'published', 'archived')");
            tableBuilder.HasCheckConstraint(
                "ck_titles_visibility",
                "visibility IN ('private', 'unlisted', 'listed')");
        });

        builder.HasKey(title => title.Id)
            .HasName("pk_titles");

        builder.Property(title => title.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(title => title.OrganizationId)
            .HasColumnName("organization_id")
            .ValueGeneratedNever();

        builder.Property(title => title.Slug)
            .HasColumnName("slug")
            .HasMaxLength(100)
            .IsRequired()
            .HasComment("Human-readable unique route key scoped to the owning organization.");

        builder.Property(title => title.ContentKind)
            .HasColumnName("content_kind")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(title => title.LifecycleStatus)
            .HasColumnName("lifecycle_status")
            .HasMaxLength(20)
            .IsRequired()
            .HasComment("Catalog lifecycle state used to control developer and public visibility.");

        builder.Property(title => title.Visibility)
            .HasColumnName("visibility")
            .HasMaxLength(20)
            .IsRequired()
            .HasComment("Public discoverability for routes and listing behavior.");

        builder.Property(title => title.CurrentMetadataVersionId)
            .HasColumnName("current_metadata_version_id");

        builder.Property(title => title.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(title => title.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(title => title.OrganizationId)
            .HasDatabaseName("ix_titles_organization_id");

        builder.HasIndex(title => new { title.OrganizationId, title.Slug })
            .IsUnique()
            .HasDatabaseName("ux_titles_organization_id_slug");

        builder.HasOne(title => title.Organization)
            .WithMany(organization => organization.Titles)
            .HasForeignKey(title => title.OrganizationId)
            .HasConstraintName("fk_titles_organizations_organization_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(title => title.CurrentMetadataVersion)
            .WithMany()
            .HasForeignKey(title => new { title.CurrentMetadataVersionId, title.Id })
            .HasPrincipalKey(metadataVersion => new { metadataVersion.Id, metadataVersion.TitleId })
            .HasConstraintName("fk_titles_title_metadata_versions_current_metadata")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
