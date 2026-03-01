namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

internal sealed class TitleMetadataVersion
{
    public Guid Id { get; set; }

    public Guid TitleId { get; set; }

    public int RevisionNumber { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string ShortDescription { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string GenreDisplay { get; set; } = string.Empty;

    public int MinPlayers { get; set; }

    public int MaxPlayers { get; set; }

    public string AgeRatingAuthority { get; set; } = string.Empty;

    public string AgeRatingValue { get; set; } = string.Empty;

    public int MinAgeYears { get; set; }

    public bool IsFrozen { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Title Title { get; set; } = null!;
}
