namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

/// <summary>
/// Installable artifact metadata associated with a release.
/// </summary>
internal sealed class ReleaseArtifact
{
    /// <summary>
    /// Gets or sets the artifact identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the parent release identifier.
    /// </summary>
    public Guid ReleaseId { get; set; }

    /// <summary>
    /// Gets or sets the artifact kind.
    /// </summary>
    public string ArtifactKind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Android package name.
    /// </summary>
    public string PackageName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Android version code.
    /// </summary>
    public long VersionCode { get; set; }

    /// <summary>
    /// Gets or sets the optional SHA-256 checksum.
    /// </summary>
    public string? Sha256 { get; set; }

    /// <summary>
    /// Gets or sets the optional file size in bytes.
    /// </summary>
    public long? FileSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the most recent update.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the parent release.
    /// </summary>
    public TitleRelease Release { get; set; } = null!;
}
