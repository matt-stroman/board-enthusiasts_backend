namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

internal sealed class OrganizationMembership
{
    public Guid OrganizationId { get; set; }

    public Guid UserId { get; set; }

    public string Role { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Organization Organization { get; set; } = null!;

    public AppUser User { get; set; } = null!;
}
