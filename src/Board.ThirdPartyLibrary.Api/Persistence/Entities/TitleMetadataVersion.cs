namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

/// <summary>
/// Player-facing metadata snapshot for a title.
/// </summary>
internal sealed class TitleMetadataVersion
{
    /// <summary>
    /// Gets or sets the metadata revision identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the parent title identifier.
    /// </summary>
    public Guid TitleId { get; set; }

    /// <summary>
    /// Gets or sets the monotonically increasing revision number within the title.
    /// </summary>
    public int RevisionNumber { get; set; }

    /// <summary>
    /// Gets or sets the public display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the short public description.
    /// </summary>
    public string ShortDescription { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full public description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display-oriented genre text.
    /// </summary>
    public string GenreDisplay { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the minimum supported player count.
    /// </summary>
    public int MinPlayers { get; set; }

    /// <summary>
    /// Gets or sets the maximum supported player count.
    /// </summary>
    public int MaxPlayers { get; set; }

    /// <summary>
    /// Gets or sets the rating authority such as ESRB or PEGI.
    /// </summary>
    public string AgeRatingAuthority { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rating value defined by the authority.
    /// </summary>
    public string AgeRatingValue { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the minimum recommended player age.
    /// </summary>
    public int MinAgeYears { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the revision is immutable.
    /// </summary>
    public bool IsFrozen { get; set; }

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the most recent revision update.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the parent title.
    /// </summary>
    public Title Title { get; set; } = null!;

    /// <summary>
    /// Gets or sets the releases that capture this metadata revision.
    /// </summary>
    public ICollection<TitleRelease> Releases { get; set; } = [];
}
