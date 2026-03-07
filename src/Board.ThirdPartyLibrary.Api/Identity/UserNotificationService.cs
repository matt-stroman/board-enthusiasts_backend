using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Auth;
using Board.ThirdPartyLibrary.Api.Persistence;
using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Board.ThirdPartyLibrary.Api.Studios;
using Microsoft.EntityFrameworkCore;

namespace Board.ThirdPartyLibrary.Api.Identity;

/// <summary>
/// Service contract for in-app notification workflows.
/// </summary>
internal interface IUserNotificationService
{
    /// <summary>
    /// Lists recent notifications for the current authenticated user.
    /// </summary>
    Task<UserNotificationListResult> GetCurrentUserNotificationsAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a current-user notification as read.
    /// </summary>
    Task<UserNotificationMutationResult> MarkCurrentUserNotificationReadAsync(IEnumerable<Claim> claims, Guid notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists notification recipients for moderator-facing workflows.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetModeratorRecipientIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists notification recipients who can manage reports for the supplied studio.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetStudioReportManagerRecipientIdsAsync(Guid studioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates notifications for the supplied user identifiers.
    /// </summary>
    Task CreateNotificationsAsync(IEnumerable<Guid> userIds, string category, string title, string body, string? actionUrl, CancellationToken cancellationToken = default);
}

/// <summary>
/// Entity Framework-backed implementation of <see cref="IUserNotificationService" />.
/// </summary>
internal sealed class UserNotificationService(
    BoardLibraryDbContext dbContext,
    IIdentityPersistenceService identityPersistenceService) : IUserNotificationService
{
    private const int DefaultNotificationListLimit = 40;

    public async Task<UserNotificationListResult> GetCurrentUserNotificationsAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var notifications = await dbContext.UserNotifications
            .AsNoTracking()
            .Where(candidate => candidate.UserId == actor.Id)
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .Take(DefaultNotificationListLimit)
            .Select(candidate => MapSnapshot(candidate))
            .ToListAsync(cancellationToken);

        return new UserNotificationListResult(UserNotificationListStatus.Success, notifications);
    }

    public async Task<UserNotificationMutationResult> MarkCurrentUserNotificationReadAsync(IEnumerable<Claim> claims, Guid notificationId, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var notification = await dbContext.UserNotifications
            .SingleOrDefaultAsync(candidate => candidate.Id == notificationId && candidate.UserId == actor.Id, cancellationToken);
        if (notification is null)
        {
            return new UserNotificationMutationResult(UserNotificationMutationStatus.NotFound);
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTime.UtcNow;
            notification.UpdatedAtUtc = notification.ReadAtUtc.Value;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new UserNotificationMutationResult(UserNotificationMutationStatus.Success, MapSnapshot(notification));
    }

    public async Task<IReadOnlyList<Guid>> GetModeratorRecipientIdsAsync(CancellationToken cancellationToken = default) =>
        await dbContext.UserPlatformRoles
            .AsNoTracking()
            .Where(candidate =>
                string.Equals(candidate.Role, PlatformRoleNames.Moderator, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.Role, PlatformRoleNames.Admin, StringComparison.OrdinalIgnoreCase))
            .Select(candidate => candidate.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetStudioReportManagerRecipientIdsAsync(Guid studioId, CancellationToken cancellationToken = default) =>
        await dbContext.StudioMemberships
            .AsNoTracking()
            .Where(candidate =>
                candidate.StudioId == studioId &&
                (string.Equals(candidate.Role, StudioRoles.Owner, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(candidate.Role, StudioRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(candidate.Role, StudioRoles.Editor, StringComparison.OrdinalIgnoreCase)))
            .Select(candidate => candidate.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task CreateNotificationsAsync(IEnumerable<Guid> userIds, string category, string title, string body, string? actionUrl, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var distinctUserIds = userIds
            .Where(candidate => candidate != Guid.Empty)
            .Distinct()
            .ToArray();
        if (distinctUserIds.Length == 0)
        {
            return;
        }

        dbContext.UserNotifications.AddRange(
            distinctUserIds.Select(userId => new UserNotification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Category = category,
                Title = title,
                Body = body,
                ActionUrl = actionUrl,
                IsRead = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            }));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<AppUser> EnsureActorAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        await identityPersistenceService.EnsureCurrentUserProjectionAsync(claims, cancellationToken);
        var subject = ClaimValueResolver.GetSubject(claims);
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new InvalidOperationException("Authenticated user is missing the required subject claim.");
        }

        return await dbContext.Users.SingleAsync(candidate => candidate.KeycloakSubject == subject, cancellationToken);
    }

    private static UserNotificationSnapshot MapSnapshot(UserNotification notification) =>
        new(
            notification.Id,
            notification.Category,
            notification.Title,
            notification.Body,
            notification.ActionUrl,
            notification.IsRead,
            notification.ReadAtUtc,
            notification.CreatedAtUtc,
            notification.UpdatedAtUtc);
}

/// <summary>
/// Local platform-role codes used for notification targeting.
/// </summary>
internal static class PlatformRoleNames
{
    public const string Admin = "admin";
    public const string Moderator = "moderator";
}

/// <summary>
/// Snapshot returned by notification endpoints.
/// </summary>
internal sealed record UserNotificationSnapshot(
    Guid Id,
    string Category,
    string Title,
    string Body,
    string? ActionUrl,
    bool IsRead,
    DateTime? ReadAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>
/// Notification list result wrapper.
/// </summary>
internal sealed record UserNotificationListResult(UserNotificationListStatus Status, IReadOnlyList<UserNotificationSnapshot>? Notifications = null);

/// <summary>
/// Notification mutation result wrapper.
/// </summary>
internal sealed record UserNotificationMutationResult(UserNotificationMutationStatus Status, UserNotificationSnapshot? Notification = null);

/// <summary>
/// Notification list statuses.
/// </summary>
internal enum UserNotificationListStatus
{
    Success
}

/// <summary>
/// Notification mutation statuses.
/// </summary>
internal enum UserNotificationMutationStatus
{
    Success,
    NotFound
}
