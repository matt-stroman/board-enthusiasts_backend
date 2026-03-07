namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

/// <summary>
/// Studio-owned reusable connection to a supported or custom publisher/store.
/// </summary>
internal sealed class IntegrationConnection
{
    /// <summary>
    /// Gets or sets the integration connection identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the owning studio identifier.
    /// </summary>
    public Guid StudioId { get; set; }

    /// <summary>
    /// Gets or sets the optional supported publisher identifier.
    /// </summary>
    public Guid? SupportedPublisherId { get; set; }

    /// <summary>
    /// Gets or sets the custom publisher display name when no supported publisher is used.
    /// </summary>
    public string? CustomPublisherDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the custom publisher homepage URL when no supported publisher is used.
    /// </summary>
    public string? CustomPublisherHomepageUrl { get; set; }

    /// <summary>
    /// Gets or sets free-form provider-specific configuration values.
    /// </summary>
    public string? ConfigurationJson { get; set; }

    /// <summary>
    /// Gets or sets whether the connection is enabled for use by bindings.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC update timestamp.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the owning studio.
    /// </summary>
    public Studio Studio { get; set; } = null!;

    /// <summary>
    /// Gets or sets the referenced supported publisher when present.
    /// </summary>
    public SupportedPublisher? SupportedPublisher { get; set; }

    /// <summary>
    /// Gets or sets title bindings that point at this connection.
    /// </summary>
    public ICollection<TitleIntegrationBinding> TitleIntegrationBindings { get; set; } = [];
}
