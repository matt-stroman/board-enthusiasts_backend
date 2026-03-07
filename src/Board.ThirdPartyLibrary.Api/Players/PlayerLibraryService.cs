using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Auth;
using Board.ThirdPartyLibrary.Api.Identity;
using Board.ThirdPartyLibrary.Api.Persistence;
using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Board.ThirdPartyLibrary.Api.Titles;
using Microsoft.EntityFrameworkCore;

namespace Board.ThirdPartyLibrary.Api.Players;

/// <summary>
/// Service contract for authenticated player library, wishlist, and reporting workflows.
/// </summary>
internal interface IPlayerLibraryService
{
    /// <summary>
    /// Lists owned titles for the current player.
    /// </summary>
    Task<PlayerTitleListResult> GetLibraryAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a title to the current player's owned library.
    /// </summary>
    Task<PlayerCollectionMutationResult> AddToLibraryAsync(IEnumerable<Claim> claims, Guid titleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a title from the current player's owned library.
    /// </summary>
    Task<PlayerCollectionMutationResult> RemoveFromLibraryAsync(IEnumerable<Claim> claims, Guid titleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists wishlist titles for the current player.
    /// </summary>
    Task<PlayerTitleListResult> GetWishlistAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a title to the current player's wishlist.
    /// </summary>
    Task<PlayerCollectionMutationResult> AddToWishlistAsync(IEnumerable<Claim> claims, Guid titleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a title from the current player's wishlist.
    /// </summary>
    Task<PlayerCollectionMutationResult> RemoveFromWishlistAsync(IEnumerable<Claim> claims, Guid titleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists title reports submitted by the current player.
    /// </summary>
    Task<PlayerReportListResult> GetReportsAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new title report for the current player.
    /// </summary>
    Task<PlayerReportMutationResult> CreateReportAsync(IEnumerable<Claim> claims, CreatePlayerTitleReportCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Entity Framework-backed implementation of <see cref="IPlayerLibraryService" />.
/// </summary>
internal sealed class PlayerLibraryService(
    BoardLibraryDbContext dbContext,
    IIdentityPersistenceService identityPersistenceService,
    IUserNotificationService userNotificationService) : IPlayerLibraryService
{
    public async Task<PlayerTitleListResult> GetLibraryAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var titles = await dbContext.PlayerOwnedTitles
            .AsNoTracking()
            .Where(entry => entry.UserId == actor.Id)
            .Include(entry => entry.Title)
                .ThenInclude(title => title.Studio)
            .Include(entry => entry.Title)
                .ThenInclude(title => title.CurrentMetadataVersion)
            .Include(entry => entry.Title)
                .ThenInclude(title => title.MediaAssets)
            .Include(entry => entry.Title)
                .ThenInclude(title => title.IntegrationBindings)
                    .ThenInclude(binding => binding.IntegrationConnection)
                        .ThenInclude(connection => connection.SupportedPublisher)
            .Include(entry => entry.Title)
                .ThenInclude(title => title.CurrentRelease)
                    .ThenInclude(release => release!.MetadataVersion)
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .Select(entry => TitleSnapshotMapper.MapTitle(entry.Title, includeDescription: false))
            .ToListAsync(cancellationToken);

        return new PlayerTitleListResult(PlayerTitleListStatus.Success, titles);
    }

    public Task<PlayerCollectionMutationResult> AddToLibraryAsync(IEnumerable<Claim> claims, Guid titleId, CancellationToken cancellationToken = default) =>
        AddCollectionEntryAsync(
            claims,
            titleId,
            dbContext.PlayerOwnedTitles,
            (userId, candidateTitleId, now) => new PlayerOwnedTitle
            {
                UserId = userId,
                TitleId = candidateTitleId,
                CreatedAtUtc = now
            },
            cancellationToken);

    public Task<PlayerCollectionMutationResult> RemoveFromLibraryAsync(IEnumerable<Claim> claims, Guid titleId, CancellationToken cancellationToken = default) =>
        RemoveCollectionEntryAsync(claims, titleId, dbContext.PlayerOwnedTitles, cancellationToken);

    public async Task<PlayerTitleListResult> GetWishlistAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var titles = await dbContext.PlayerWishlistEntries
            .AsNoTracking()
            .Where(entry => entry.UserId == actor.Id)
            .Include(entry => entry.Title)
                .ThenInclude(title => title.Studio)
            .Include(entry => entry.Title)
                .ThenInclude(title => title.CurrentMetadataVersion)
            .Include(entry => entry.Title)
                .ThenInclude(title => title.MediaAssets)
            .Include(entry => entry.Title)
                .ThenInclude(title => title.IntegrationBindings)
                    .ThenInclude(binding => binding.IntegrationConnection)
                        .ThenInclude(connection => connection.SupportedPublisher)
            .Include(entry => entry.Title)
                .ThenInclude(title => title.CurrentRelease)
                    .ThenInclude(release => release!.MetadataVersion)
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .Select(entry => TitleSnapshotMapper.MapTitle(entry.Title, includeDescription: false))
            .ToListAsync(cancellationToken);

        return new PlayerTitleListResult(PlayerTitleListStatus.Success, titles);
    }

    public Task<PlayerCollectionMutationResult> AddToWishlistAsync(IEnumerable<Claim> claims, Guid titleId, CancellationToken cancellationToken = default) =>
        AddCollectionEntryAsync(
            claims,
            titleId,
            dbContext.PlayerWishlistEntries,
            (userId, candidateTitleId, now) => new PlayerWishlistEntry
            {
                UserId = userId,
                TitleId = candidateTitleId,
                CreatedAtUtc = now
            },
            cancellationToken);

    public Task<PlayerCollectionMutationResult> RemoveFromWishlistAsync(IEnumerable<Claim> claims, Guid titleId, CancellationToken cancellationToken = default) =>
        RemoveCollectionEntryAsync(claims, titleId, dbContext.PlayerWishlistEntries, cancellationToken);

    public async Task<PlayerReportListResult> GetReportsAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var reports = await dbContext.TitleReports
            .AsNoTracking()
            .Where(report => report.ReporterUserId == actor.Id)
            .Include(report => report.Title)
                .ThenInclude(title => title.Studio)
            .Include(report => report.Title)
                .ThenInclude(title => title.CurrentMetadataVersion)
            .OrderByDescending(report => report.UpdatedAtUtc)
            .Select(report => new PlayerTitleReportSummarySnapshot(
                report.Id,
                report.TitleId,
                report.Title.Studio.Slug,
                report.Title.Slug,
                report.Title.CurrentMetadataVersion!.DisplayName,
                report.Status,
                report.Reason,
                report.CreatedAtUtc,
                report.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PlayerReportListResult(PlayerReportListStatus.Success, reports);
    }

    public async Task<PlayerReportMutationResult> CreateReportAsync(
        IEnumerable<Claim> claims,
        CreatePlayerTitleReportCommand command,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var title = await dbContext.Titles
            .Include(candidate => candidate.Studio)
            .Include(candidate => candidate.CurrentMetadataVersion)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == command.TitleId && candidate.CurrentMetadataVersionId != null,
                cancellationToken);

        if (title is null || title.CurrentMetadataVersion is null)
        {
            return new PlayerReportMutationResult(PlayerReportMutationStatus.NotFound);
        }

        var hasOpenReport = await dbContext.TitleReports.AnyAsync(
            report =>
                report.ReporterUserId == actor.Id &&
                report.TitleId == title.Id &&
                (report.Status == TitleReportStatuses.Open ||
                 report.Status == TitleReportStatuses.NeedsDeveloperResponse ||
                 report.Status == TitleReportStatuses.DeveloperResponded ||
                 report.Status == TitleReportStatuses.NeedsPlayerResponse ||
                 report.Status == TitleReportStatuses.PlayerResponded),
            cancellationToken);
        if (hasOpenReport)
        {
            return new PlayerReportMutationResult(PlayerReportMutationStatus.Conflict);
        }

        var now = DateTime.UtcNow;
        var report = new TitleReport
        {
            Id = Guid.NewGuid(),
            TitleId = title.Id,
            ReporterUserId = actor.Id,
            Status = TitleReportStatuses.Open,
            Reason = command.Reason,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.TitleReports.Add(report);
        await dbContext.SaveChangesAsync(cancellationToken);

        var developerRecipients = (await userNotificationService.GetStudioReportManagerRecipientIdsAsync(title.StudioId, cancellationToken))
            .Where(candidate => candidate != actor.Id)
            .ToArray();
        await userNotificationService.CreateNotificationsAsync(
            developerRecipients,
            category: "title_report",
            title: "New title report",
            body: $"{GetActorDisplayName(actor)} reported {title.CurrentMetadataVersion.DisplayName} for moderation review.",
            actionUrl: BuildDeveloperReportActionUrl(title.Id, report.Id),
            cancellationToken);

        var moderatorRecipients = (await userNotificationService.GetModeratorRecipientIdsAsync(cancellationToken))
            .Where(candidate => candidate != actor.Id)
            .ToArray();
        await userNotificationService.CreateNotificationsAsync(
            moderatorRecipients,
            category: "title_report",
            title: "New reported title",
            body: $"{title.CurrentMetadataVersion.DisplayName} was reported and needs moderation review.",
            actionUrl: BuildModeratorReportActionUrl(report.Id),
            cancellationToken);

        return new PlayerReportMutationResult(
            PlayerReportMutationStatus.Success,
            new PlayerTitleReportSummarySnapshot(
                report.Id,
                report.TitleId,
                title.Studio.Slug,
                title.Slug,
                title.CurrentMetadataVersion.DisplayName,
                report.Status,
                report.Reason,
                report.CreatedAtUtc,
                report.UpdatedAtUtc));
    }

    private async Task<PlayerCollectionMutationResult> AddCollectionEntryAsync<TEntity>(
        IEnumerable<Claim> claims,
        Guid titleId,
        DbSet<TEntity> set,
        Func<Guid, Guid, DateTime, TEntity> factory,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var titleExists = await dbContext.Titles
            .AsNoTracking()
            .AnyAsync(title => title.Id == titleId && title.CurrentMetadataVersionId != null, cancellationToken);
        if (!titleExists)
        {
            return new PlayerCollectionMutationResult(PlayerCollectionMutationStatus.NotFound);
        }

        var alreadyExists = await set.FindAsync([actor.Id, titleId], cancellationToken);
        if (alreadyExists is not null)
        {
            return new PlayerCollectionMutationResult(PlayerCollectionMutationStatus.Success, AlreadyInRequestedState: true);
        }

        set.Add(factory(actor.Id, titleId, DateTime.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new PlayerCollectionMutationResult(PlayerCollectionMutationStatus.Success);
    }

    private async Task<PlayerCollectionMutationResult> RemoveCollectionEntryAsync<TEntity>(
        IEnumerable<Claim> claims,
        Guid titleId,
        DbSet<TEntity> set,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var existing = await set.FindAsync([actor.Id, titleId], cancellationToken);
        if (existing is null)
        {
            return new PlayerCollectionMutationResult(PlayerCollectionMutationStatus.Success, AlreadyInRequestedState: true);
        }

        set.Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new PlayerCollectionMutationResult(PlayerCollectionMutationStatus.Success);
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

    private static string GetActorDisplayName(AppUser actor) =>
        actor.DisplayName
        ?? actor.UserName
        ?? actor.Email
        ?? "A player";

    private static string BuildModeratorReportActionUrl(Guid reportId) =>
        $"/moderate?workflow=reports-review&reportId={Uri.EscapeDataString(reportId.ToString("D"))}";

    private static string BuildDeveloperReportActionUrl(Guid titleId, Guid reportId) =>
        $"/develop?domain=titles&workflow=titles-reports&titleId={Uri.EscapeDataString(titleId.ToString("D"))}&reportId={Uri.EscapeDataString(reportId.ToString("D"))}";
}

/// <summary>
/// Request command for creating a player-submitted title report.
/// </summary>
internal sealed record CreatePlayerTitleReportCommand(Guid TitleId, string Reason);

/// <summary>
/// Player-owned title collection list result.
/// </summary>
internal sealed record PlayerTitleListResult(PlayerTitleListStatus Status, IReadOnlyList<TitleSnapshot>? Titles = null);

/// <summary>
/// Player-owned report list result.
/// </summary>
internal sealed record PlayerReportListResult(PlayerReportListStatus Status, IReadOnlyList<PlayerTitleReportSummarySnapshot>? Reports = null);

/// <summary>
/// Collection mutation result.
/// </summary>
internal sealed record PlayerCollectionMutationResult(PlayerCollectionMutationStatus Status, bool AlreadyInRequestedState = false);

/// <summary>
/// Player report mutation result.
/// </summary>
internal sealed record PlayerReportMutationResult(PlayerReportMutationStatus Status, PlayerTitleReportSummarySnapshot? Report = null);

/// <summary>
/// Player-submitted title report summary.
/// </summary>
internal sealed record PlayerTitleReportSummarySnapshot(
    Guid Id,
    Guid TitleId,
    string StudioSlug,
    string TitleSlug,
    string TitleDisplayName,
    string Status,
    string Reason,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>
/// Player title list result status.
/// </summary>
internal enum PlayerTitleListStatus
{
    Success
}

/// <summary>
/// Player report list result status.
/// </summary>
internal enum PlayerReportListStatus
{
    Success
}

/// <summary>
/// Collection mutation result status.
/// </summary>
internal enum PlayerCollectionMutationStatus
{
    Success,
    NotFound
}

/// <summary>
/// Player report mutation result status.
/// </summary>
internal enum PlayerReportMutationStatus
{
    Success,
    NotFound,
    Conflict
}

/// <summary>
/// Known title-report workflow statuses.
/// </summary>
internal static class TitleReportStatuses
{
    /// <summary>
    /// Newly created player report awaiting moderation follow-up.
    /// </summary>
    public const string Open = "open";

    /// <summary>
    /// Moderator has asked the developer for a response.
    /// </summary>
    public const string NeedsDeveloperResponse = "needs_developer_response";

    /// <summary>
    /// Developer has responded and the report awaits moderation review.
    /// </summary>
    public const string DeveloperResponded = "developer_responded";

    /// <summary>
    /// Moderator has asked the reporting player for additional information.
    /// </summary>
    public const string NeedsPlayerResponse = "needs_player_response";

    /// <summary>
    /// Reporting player has responded and the report awaits moderation review.
    /// </summary>
    public const string PlayerResponded = "player_responded";

    /// <summary>
    /// Moderator validated the report.
    /// </summary>
    public const string Validated = "validated";

    /// <summary>
    /// Moderator invalidated the report.
    /// </summary>
    public const string Invalidated = "invalidated";
}

/// <summary>
/// Author-role values used by title-report messages.
/// </summary>
internal static class TitleReportAuthorRoles
{
    /// <summary>
    /// Moderator-authored message.
    /// </summary>
    public const string Moderator = "moderator";

    /// <summary>
    /// Developer-authored message.
    /// </summary>
    public const string Developer = "developer";

    /// <summary>
    /// Player-authored message.
    /// </summary>
    public const string Player = "player";
}
