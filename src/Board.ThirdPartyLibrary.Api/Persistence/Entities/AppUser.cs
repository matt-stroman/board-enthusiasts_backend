namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

internal sealed class AppUser
{
    public Guid Id { get; set; }

    public string KeycloakSubject { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public string? Email { get; set; }

    public bool EmailVerified { get; set; }

    public string? IdentityProvider { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public UserBoardProfile? BoardProfile { get; set; }

    public ICollection<OrganizationMembership> OrganizationMemberships { get; set; } = [];
}
