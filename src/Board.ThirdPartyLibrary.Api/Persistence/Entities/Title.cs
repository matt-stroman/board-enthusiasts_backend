namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

/// <summary>
/// Stable catalog title owned by a developer studio.
/// </summary>
internal sealed class Title
{
    /// <summary>
    /// Gets or sets the title identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the owning studio identifier.
    /// </summary>
    public Guid StudioId { get; set; }

    /// <summary>
    /// Gets or sets the studio-scoped route key for the title.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stable content kind such as game or app.
    /// </summary>
    public string ContentKind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the lifecycle state used for catalog behavior.
    /// </summary>
    public string LifecycleStatus { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the public discoverability mode for the title.
    /// </summary>
    public string Visibility { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the currently active metadata revision identifier.
    /// </summary>
    public Guid? CurrentMetadataVersionId { get; set; }

    /// <summary>
    /// Gets or sets the currently active release identifier.
    /// </summary>
    public Guid? CurrentReleaseId { get; set; }

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the most recent title-level update.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the owning studio.
    /// </summary>
    public Studio Studio { get; set; } = null!;

    /// <summary>
    /// Gets or sets the currently active metadata revision.
    /// </summary>
    public TitleMetadataVersion? CurrentMetadataVersion { get; set; }

    /// <summary>
    /// Gets or sets the currently active release.
    /// </summary>
    public TitleRelease? CurrentRelease { get; set; }

    /// <summary>
    /// Gets or sets all metadata revisions associated with the title.
    /// </summary>
    public ICollection<TitleMetadataVersion> MetadataVersions { get; set; } = [];

    /// <summary>
    /// Gets or sets all media assets associated with the title.
    /// </summary>
    public ICollection<TitleMediaAsset> MediaAssets { get; set; } = [];

    /// <summary>
    /// Gets or sets all releases associated with the title.
    /// </summary>
    public ICollection<TitleRelease> Releases { get; set; } = [];

    /// <summary>
    /// Gets or sets all external acquisition bindings associated with the title.
    /// </summary>
    public ICollection<TitleIntegrationBinding> IntegrationBindings { get; set; } = [];

    /// <summary>
    /// Gets or sets all owned-library entries associated with the title.
    /// </summary>
    public ICollection<PlayerOwnedTitle> OwnedByUsers { get; set; } = [];

    /// <summary>
    /// Gets or sets all wishlist entries associated with the title.
    /// </summary>
    public ICollection<PlayerWishlistEntry> WishlistedByUsers { get; set; } = [];

    /// <summary>
    /// Gets or sets moderation reports associated with the title.
    /// </summary>
    public ICollection<TitleReport> Reports { get; set; } = [];
}
