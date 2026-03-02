namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

/// <summary>
/// Versioned release record for a title.
/// </summary>
internal sealed class TitleRelease
{
    /// <summary>
    /// Gets or sets the release identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the owning title identifier.
    /// </summary>
    public Guid TitleId { get; set; }

    /// <summary>
    /// Gets or sets the metadata snapshot identifier captured by the release.
    /// </summary>
    public Guid MetadataVersionId { get; set; }

    /// <summary>
    /// Gets or sets the public semver release string.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the release lifecycle status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC publication timestamp when the release is published.
    /// </summary>
    public DateTime? PublishedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the most recent update.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the owning title.
    /// </summary>
    public Title Title { get; set; } = null!;

    /// <summary>
    /// Gets or sets the metadata snapshot captured by the release.
    /// </summary>
    public TitleMetadataVersion MetadataVersion { get; set; } = null!;

    /// <summary>
    /// Gets or sets the artifacts associated with the release.
    /// </summary>
    public ICollection<ReleaseArtifact> Artifacts { get; set; } = [];
}
