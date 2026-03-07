namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

/// <summary>
/// Public link associated with a studio profile.
/// </summary>
internal sealed class StudioLink
{
    public Guid Id { get; set; }

    public Guid StudioId { get; set; }

    public Studio Studio { get; set; } = null!;

    public string Label { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
