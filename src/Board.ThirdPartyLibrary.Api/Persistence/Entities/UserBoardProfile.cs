namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

internal sealed class UserBoardProfile
{
    public Guid UserId { get; set; }

    public string BoardUserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public DateTime LinkedAtUtc { get; set; }

    public DateTime LastSyncedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public AppUser User { get; set; } = null!;
}
