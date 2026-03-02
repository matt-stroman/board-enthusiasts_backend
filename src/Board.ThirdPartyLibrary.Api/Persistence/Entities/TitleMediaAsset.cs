namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

/// <summary>
/// Fixed-slot media asset associated with a catalog title.
/// </summary>
internal sealed class TitleMediaAsset
{
    /// <summary>
    /// Gets or sets the media asset identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the owning title identifier.
    /// </summary>
    public Guid TitleId { get; set; }

    /// <summary>
    /// Gets or sets the fixed Board-style media role.
    /// </summary>
    public string MediaRole { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the absolute source URL for the media asset.
    /// </summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional alt text for the media asset.
    /// </summary>
    public string? AltText { get; set; }

    /// <summary>
    /// Gets or sets the optional MIME type for the media asset.
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// Gets or sets the optional pixel width.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Gets or sets the optional pixel height.
    /// </summary>
    public int? Height { get; set; }

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
}
