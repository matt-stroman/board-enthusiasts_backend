namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

internal sealed class Organization
{
    public Guid Id { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? LogoUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<OrganizationMembership> Memberships { get; set; } = [];

    public ICollection<Title> Titles { get; set; } = [];
}
