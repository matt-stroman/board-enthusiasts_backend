using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for the <see cref="ReleaseArtifact" /> entity.
/// </summary>
internal sealed class ReleaseArtifactConfiguration : IEntityTypeConfiguration<ReleaseArtifact>
{
    /// <summary>
    /// Configures the relational mapping for release artifacts.
    /// </summary>
    /// <param name="builder">Entity builder for <see cref="ReleaseArtifact" />.</param>
    public void Configure(EntityTypeBuilder<ReleaseArtifact> builder)
    {
        builder.ToTable("release_artifacts", tableBuilder =>
        {
            tableBuilder.HasComment("Installable artifact metadata for title releases.");
            tableBuilder.HasCheckConstraint(
                "ck_release_artifacts_artifact_kind",
                "artifact_kind IN ('apk')");
            tableBuilder.HasCheckConstraint(
                "ck_release_artifacts_version_code",
                "version_code > 0");
            tableBuilder.HasCheckConstraint(
                "ck_release_artifacts_file_size_bytes",
                "file_size_bytes IS NULL OR file_size_bytes > 0");
        });

        builder.HasKey(artifact => artifact.Id)
            .HasName("pk_release_artifacts");

        builder.Property(artifact => artifact.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(artifact => artifact.ReleaseId)
            .HasColumnName("release_id")
            .ValueGeneratedNever();

        builder.Property(artifact => artifact.ArtifactKind)
            .HasColumnName("artifact_kind")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(artifact => artifact.PackageName)
            .HasColumnName("package_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(artifact => artifact.VersionCode)
            .HasColumnName("version_code")
            .IsRequired();

        builder.Property(artifact => artifact.Sha256)
            .HasColumnName("sha256")
            .HasMaxLength(64);

        builder.Property(artifact => artifact.FileSizeBytes)
            .HasColumnName("file_size_bytes");

        builder.Property(artifact => artifact.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(artifact => artifact.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(artifact => artifact.ReleaseId)
            .HasDatabaseName("ix_release_artifacts_release_id");

        builder.HasIndex(artifact => new { artifact.ReleaseId, artifact.PackageName, artifact.VersionCode })
            .IsUnique()
            .HasDatabaseName("ux_release_artifacts_release_id_package_name_version_code");

        builder.HasOne(artifact => artifact.Release)
            .WithMany(release => release.Artifacts)
            .HasForeignKey(artifact => artifact.ReleaseId)
            .HasConstraintName("fk_release_artifacts_title_releases_release_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
