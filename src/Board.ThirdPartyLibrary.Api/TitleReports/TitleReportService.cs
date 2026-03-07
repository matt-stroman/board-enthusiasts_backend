using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Auth;
using Board.ThirdPartyLibrary.Api.Identity;
using Board.ThirdPartyLibrary.Api.Persistence;
using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Board.ThirdPartyLibrary.Api.Players;
using Board.ThirdPartyLibrary.Api.Titles;
using Microsoft.EntityFrameworkCore;

namespace Board.ThirdPartyLibrary.Api.TitleReports;

/// <summary>
/// Service contract for moderation and developer title-report review workflows.
/// </summary>
internal interface ITitleReportService
{
    /// <summary>
    /// Lists all title reports visible to moderators.
    /// </summary>
    Task<TitleReportListResult> GetModerationReportsAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single title report for moderator review.
    /// </summary>
    Task<TitleReportMutationResult> GetModerationReportAsync(IEnumerable<Claim> claims, Guid reportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a moderator-authored message to a title report thread.
    /// </summary>
    Task<TitleReportMutationResult> AddModeratorMessageAsync(IEnumerable<Claim> claims, Guid reportId, string message, string recipientRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a title report and removes the title from public visibility.
    /// </summary>
    Task<TitleReportMutationResult> ValidateReportAsync(IEnumerable<Claim> claims, Guid reportId, string? note, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates a title report without removing the title.
    /// </summary>
    Task<TitleReportMutationResult> InvalidateReportAsync(IEnumerable<Claim> claims, Guid reportId, string? note, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists title reports for a developer-managed title.
    /// </summary>
    Task<TitleReportListResult> GetDeveloperReportsAsync(IEnumerable<Claim> claims, Guid titleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single title report for a developer-managed title.
    /// </summary>
    Task<TitleReportMutationResult> GetDeveloperReportAsync(IEnumerable<Claim> claims, Guid titleId, Guid reportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a developer-authored message to a title report thread.
    /// </summary>
    Task<TitleReportMutationResult> AddDeveloperMessageAsync(IEnumerable<Claim> claims, Guid titleId, Guid reportId, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single title report for the reporting player.
    /// </summary>
    Task<TitleReportMutationResult> GetPlayerReportAsync(IEnumerable<Claim> claims, Guid reportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a player-authored message to a title report thread.
    /// </summary>
    Task<TitleReportMutationResult> AddPlayerMessageAsync(IEnumerable<Claim> claims, Guid reportId, string message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Entity Framework-backed implementation of <see cref="ITitleReportService" />.
/// </summary>
internal sealed class TitleReportService(
    BoardLibraryDbContext dbContext,
    IIdentityPersistenceService identityPersistenceService,
    IUserNotificationService userNotificationService) : ITitleReportService
{
    public async Task<TitleReportListResult> GetModerationReportsAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        if (!HasModeratorAccess(claims))
        {
            return new TitleReportListResult(TitleReportListStatus.Forbidden);
        }

        var reports = await dbContext.TitleReports
            .AsNoTracking()
            .Include(report => report.Title)
                .ThenInclude(title => title.Studio)
            .Include(report => report.Title)
                .ThenInclude(title => title.CurrentMetadataVersion)
            .Include(report => report.ReporterUser)
            .Include(report => report.Messages)
            .OrderByDescending(report => report.UpdatedAtUtc)
            .Select(report => MapSummary(report))
            .ToListAsync(cancellationToken);

        return new TitleReportListResult(TitleReportListStatus.Success, reports);
    }

    public async Task<TitleReportMutationResult> GetModerationReportAsync(IEnumerable<Claim> claims, Guid reportId, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        if (!HasModeratorAccess(claims))
        {
            return new TitleReportMutationResult(TitleReportMutationStatus.Forbidden);
        }

        var report = await LoadReportAsync(reportId, cancellationToken);
        return report is null
            ? new TitleReportMutationResult(TitleReportMutationStatus.NotFound)
            : new TitleReportMutationResult(TitleReportMutationStatus.Success, MapDetail(report, TitleReportViewerRoles.Moderator));
    }

    public async Task<TitleReportMutationResult> AddModeratorMessageAsync(IEnumerable<Claim> claims, Guid reportId, string message, string recipientRole, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        if (!HasModeratorAccess(claims))
        {
            return new TitleReportMutationResult(TitleReportMutationStatus.Forbidden);
        }

        var report = await LoadReportAsync(reportId, cancellationToken);
        if (report is null)
        {
            return new TitleReportMutationResult(TitleReportMutationStatus.NotFound);
        }

        var normalizedRecipientRole = NormalizeModeratorRecipientRole(recipientRole);
        if (normalizedRecipientRole is null)
        {
            return new TitleReportMutationResult(TitleReportMutationStatus.ValidationFailed);
        }

        AddMessage(
            report,
            actor,
            TitleReportAuthorRoles.Moderator,
            normalizedRecipientRole == TitleReportAuthorRoles.Player ? TitleReportAudiences.Player : TitleReportAudiences.Developer,
            message);
        if (report.Status is not (TitleReportStatuses.Validated or TitleReportStatuses.Invalidated))
        {
            report.Status = normalizedRecipientRole == TitleReportAuthorRoles.Player
                ? TitleReportStatuses.NeedsPlayerResponse
                : TitleReportStatuses.NeedsDeveloperResponse;
        }

        report.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (normalizedRecipientRole == TitleReportAuthorRoles.Player)
        {
            await NotifyPlayerAsync(
                report,
                actor.Id,
                "Moderator follow-up on reported title",
                $"Moderation requested more information about {report.Title.CurrentMetadataVersion!.DisplayName}.",
                cancellationToken);
        }
        else
        {
            await NotifyDevelopersAsync(
                report,
                actor.Id,
                "Moderator follow-up on reported title",
                $"Moderation requested a developer response for {report.Title.CurrentMetadataVersion!.DisplayName}.",
                cancellationToken);
        }

        return new TitleReportMutationResult(TitleReportMutationStatus.Success, MapDetail(report, TitleReportViewerRoles.Moderator));
    }

    public async Task<TitleReportMutationResult> ValidateReportAsync(IEnumerable<Claim> claims, Guid reportId, string? note, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        if (!HasModeratorAccess(claims))
        {
            return new TitleReportMutationResult(TitleReportMutationStatus.Forbidden);
        }

        var report = await LoadReportAsync(reportId, cancellationToken);
        if (report is null)
        {
            return new TitleReportMutationResult(TitleReportMutationStatus.NotFound);
        }

        var now = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(note))
        {
            AddMessage(report, actor, TitleReportAuthorRoles.Moderator, TitleReportAudiences.All, note.Trim(), createdAtUtc: now);
        }

        report.Status = TitleReportStatuses.Validated;
        report.ResolutionNote = string.IsNullOrWhiteSpace(note) ? report.ResolutionNote : note.Trim();
        report.ResolvedByUserId = actor.Id;
        report.ResolvedAtUtc = now;
        report.UpdatedAtUtc = now;
        report.Title.Visibility = TitleVisibilities.Unlisted;
        report.Title.LifecycleStatus = TitleLifecycleStatuses.Testing;
        report.Title.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyDevelopersAsync(
            report,
            actor.Id,
            "Reported title was unlisted",
            $"{report.Title.CurrentMetadataVersion!.DisplayName} was validated by moderation and removed from public listings.",
            cancellationToken);
        await NotifyPlayerAsync(
            report,
            actor.Id,
            "Your title report was validated",
            $"{report.Title.CurrentMetadataVersion!.DisplayName} was validated by moderation and removed from public listings.",
            cancellationToken);
        return new TitleReportMutationResult(TitleReportMutationStatus.Success, MapDetail(report, TitleReportViewerRoles.Moderator));
    }

    public async Task<TitleReportMutationResult> InvalidateReportAsync(IEnumerable<Claim> claims, Guid reportId, string? note, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        if (!HasModeratorAccess(claims))
        {
            return new TitleReportMutationResult(TitleReportMutationStatus.Forbidden);
        }

        var report = await LoadReportAsync(reportId, cancellationToken);
        if (report is null)
        {
            return new TitleReportMutationResult(TitleReportMutationStatus.NotFound);
        }

        var now = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(note))
        {
            AddMessage(report, actor, TitleReportAuthorRoles.Moderator, TitleReportAudiences.All, note.Trim(), createdAtUtc: now);
        }

        report.Status = TitleReportStatuses.Invalidated;
        report.ResolutionNote = string.IsNullOrWhiteSpace(note) ? report.ResolutionNote : note.Trim();
        report.ResolvedByUserId = actor.Id;
        report.ResolvedAtUtc = now;
        report.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyDevelopersAsync(
            report,
            actor.Id,
            "Reported title review closed",
            $"{report.Title.CurrentMetadataVersion!.DisplayName} was reviewed and the report was invalidated.",
            cancellationToken);
        await NotifyPlayerAsync(
            report,
            actor.Id,
            "Your title report was closed",
            $"{report.Title.CurrentMetadataVersion!.DisplayName} was reviewed and the report was invalidated.",
            cancellationToken);
        return new TitleReportMutationResult(TitleReportMutationStatus.Success, MapDetail(report, TitleReportViewerRoles.Moderator));
    }

    public async Task<TitleReportListResult> GetDeveloperReportsAsync(IEnumerable<Claim> claims, Guid titleId, CancellationToken cancellationToken = default)
    {
        var access = await EnsureDeveloperTitleAccessAsync(claims, titleId, cancellationToken);
        if (access.Status is not DeveloperTitleAccessStatus.Success)
        {
            return access.Status switch
            {
                DeveloperTitleAccessStatus.Forbidden => new TitleReportListResult(TitleReportListStatus.Forbidden),
                _ => new TitleReportListResult(TitleReportListStatus.NotFound)
            };
        }

        var reports = await dbContext.TitleReports
            .AsNoTracking()
            .Where(report => report.TitleId == titleId)
            .Include(report => report.Title)
                .ThenInclude(title => title.Studio)
            .Include(report => report.Title)
                .ThenInclude(title => title.CurrentMetadataVersion)
            .Include(report => report.ReporterUser)
            .Include(report => report.Messages)
            .OrderByDescending(report => report.UpdatedAtUtc)
            .Select(report => MapSummary(report))
            .ToListAsync(cancellationToken);

        return new TitleReportListResult(TitleReportListStatus.Success, reports);
    }

    public async Task<TitleReportMutationResult> GetDeveloperReportAsync(IEnumerable<Claim> claims, Guid titleId, Guid reportId, CancellationToken cancellationToken = default)
    {
        var access = await EnsureDeveloperTitleAccessAsync(claims, titleId, cancellationToken);
        if (access.Status is not DeveloperTitleAccessStatus.Success)
        {
            return access.Status switch
            {
                DeveloperTitleAccessStatus.Forbidden => new TitleReportMutationResult(TitleReportMutationStatus.Forbidden),
                _ => new TitleReportMutationResult(TitleReportMutationStatus.NotFound)
            };
        }

        var report = await LoadReportAsync(reportId, cancellationToken);
        if (report is null || report.TitleId != titleId)
        {
            return new TitleReportMutationResult(TitleReportMutationStatus.NotFound);
        }

        return new TitleReportMutationResult(TitleReportMutationStatus.Success, MapDetail(report, TitleReportViewerRoles.Developer));
    }

    public async Task<TitleReportMutationResult> AddDeveloperMessageAsync(IEnumerable<Claim> claims, Guid titleId, Guid reportId, string message, CancellationToken cancellationToken = default)
    {
        var access = await EnsureDeveloperTitleAccessAsync(claims, titleId, cancellationToken);
        if (access.Status is not DeveloperTitleAccessStatus.Success)
        {
            return access.Status switch
            {
                DeveloperTitleAccessStatus.Forbidden => new TitleReportMutationResult(TitleReportMutationStatus.Forbidden),
                _ => new TitleReportMutationResult(TitleReportMutationStatus.NotFound)
            };
        }

        var report = await LoadReportAsync(reportId, cancellationToken);
        if (report is null || report.TitleId != titleId)
        {
            return new TitleReportMutationResult(TitleReportMutationStatus.NotFound);
        }

        AddMessage(report, access.Actor!, TitleReportAuthorRoles.Developer, TitleReportAudiences.Developer, message);
        if (report.Status is not (TitleReportStatuses.Validated or TitleReportStatuses.Invalidated))
        {
            report.Status = TitleReportStatuses.DeveloperResponded;
        }

        report.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyModeratorsAsync(
            report,
            access.Actor!.Id,
            "Developer replied to title report",
            $"{GetActorDisplayName(access.Actor)} replied on {report.Title.CurrentMetadataVersion!.DisplayName}.",
            cancellationToken);
        return new TitleReportMutationResult(TitleReportMutationStatus.Success, MapDetail(report, TitleReportViewerRoles.Developer));
    }

    public async Task<TitleReportMutationResult> GetPlayerReportAsync(IEnumerable<Claim> claims, Guid reportId, CancellationToken cancellationToken = default)
    {
        var access = await EnsurePlayerReportAccessAsync(claims, reportId, cancellationToken);
        if (access.Status is not PlayerReportAccessStatus.Success)
        {
            return access.Status switch
            {
                PlayerReportAccessStatus.Forbidden => new TitleReportMutationResult(TitleReportMutationStatus.Forbidden),
                _ => new TitleReportMutationResult(TitleReportMutationStatus.NotFound)
            };
        }

        return new TitleReportMutationResult(TitleReportMutationStatus.Success, MapDetail(access.Report!, TitleReportViewerRoles.Player));
    }

    public async Task<TitleReportMutationResult> AddPlayerMessageAsync(IEnumerable<Claim> claims, Guid reportId, string message, CancellationToken cancellationToken = default)
    {
        var access = await EnsurePlayerReportAccessAsync(claims, reportId, cancellationToken);
        if (access.Status is not PlayerReportAccessStatus.Success)
        {
            return access.Status switch
            {
                PlayerReportAccessStatus.Forbidden => new TitleReportMutationResult(TitleReportMutationStatus.Forbidden),
                _ => new TitleReportMutationResult(TitleReportMutationStatus.NotFound)
            };
        }

        AddMessage(access.Report!, access.Actor!, TitleReportAuthorRoles.Player, TitleReportAudiences.Player, message);
        if (access.Report!.Status is not (TitleReportStatuses.Validated or TitleReportStatuses.Invalidated))
        {
            access.Report.Status = TitleReportStatuses.PlayerResponded;
        }

        access.Report.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyModeratorsAsync(
            access.Report,
            access.Actor!.Id,
            "Player replied to title report",
            $"{GetActorDisplayName(access.Actor)} replied with more information about {access.Report.Title.CurrentMetadataVersion!.DisplayName}.",
            cancellationToken);
        return new TitleReportMutationResult(TitleReportMutationStatus.Success, MapDetail(access.Report, TitleReportViewerRoles.Player));
    }

    private async Task<TitleReport?> LoadReportAsync(Guid reportId, CancellationToken cancellationToken) =>
        await dbContext.TitleReports
            .Include(report => report.Title)
                .ThenInclude(title => title.Studio)
            .Include(report => report.Title)
                .ThenInclude(title => title.CurrentMetadataVersion)
            .Include(report => report.ReporterUser)
            .Include(report => report.ResolvedByUser)
            .Include(report => report.Messages.OrderBy(message => message.CreatedAtUtc))
                .ThenInclude(message => message.AuthorUser)
            .SingleOrDefaultAsync(report => report.Id == reportId, cancellationToken);

    private static void AddMessage(
        TitleReport report,
        AppUser actor,
        string authorRole,
        string audience,
        string message,
        DateTime? createdAtUtc = null)
    {
        var createdAt = createdAtUtc ?? DateTime.UtcNow;
        report.Messages.Add(new TitleReportMessage
        {
            Id = Guid.NewGuid(),
            TitleReportId = report.Id,
            AuthorUserId = actor.Id,
            AuthorRole = authorRole,
            Audience = audience,
            Message = message,
            CreatedAtUtc = createdAt
        });
    }

    private async Task NotifyDevelopersAsync(TitleReport report, Guid actorUserId, string title, string body, CancellationToken cancellationToken)
    {
        var recipientIds = (await userNotificationService.GetStudioReportManagerRecipientIdsAsync(report.Title.StudioId, cancellationToken))
            .Where(candidate => candidate != actorUserId)
            .ToArray();
        await userNotificationService.CreateNotificationsAsync(
            recipientIds,
            category: "title_report",
            title: title,
            body: body,
            actionUrl: BuildDeveloperReportActionUrl(report.TitleId, report.Id),
            cancellationToken);
    }

    private async Task NotifyModeratorsAsync(TitleReport report, Guid actorUserId, string title, string body, CancellationToken cancellationToken)
    {
        var recipientIds = (await userNotificationService.GetModeratorRecipientIdsAsync(cancellationToken))
            .Where(candidate => candidate != actorUserId)
            .ToArray();
        await userNotificationService.CreateNotificationsAsync(
            recipientIds,
            category: "title_report",
            title: title,
            body: body,
            actionUrl: BuildModeratorReportActionUrl(report.Id),
            cancellationToken);
    }

    private async Task NotifyPlayerAsync(TitleReport report, Guid actorUserId, string title, string body, CancellationToken cancellationToken)
    {
        if (report.ReporterUserId == actorUserId)
        {
            return;
        }

        await userNotificationService.CreateNotificationsAsync(
            [report.ReporterUserId],
            category: "title_report",
            title: title,
            body: body,
            actionUrl: BuildPlayerReportActionUrl(report.Id),
            cancellationToken);
    }

    private async Task<DeveloperTitleAccessResult> EnsureDeveloperTitleAccessAsync(IEnumerable<Claim> claims, Guid titleId, CancellationToken cancellationToken)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var title = await dbContext.Titles
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == titleId, cancellationToken);
        if (title is null)
        {
            return new DeveloperTitleAccessResult(DeveloperTitleAccessStatus.NotFound);
        }

        var actorRole = await dbContext.StudioMemberships
            .AsNoTracking()
            .Where(candidate => candidate.StudioId == title.StudioId && candidate.UserId == actor.Id)
            .Select(candidate => candidate.Role)
            .SingleOrDefaultAsync(cancellationToken);
        if (!CanManageTitleReports(actorRole))
        {
            return new DeveloperTitleAccessResult(DeveloperTitleAccessStatus.Forbidden);
        }

        return new DeveloperTitleAccessResult(DeveloperTitleAccessStatus.Success, actor);
    }

    private async Task<PlayerReportAccessResult> EnsurePlayerReportAccessAsync(IEnumerable<Claim> claims, Guid reportId, CancellationToken cancellationToken)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var report = await LoadReportAsync(reportId, cancellationToken);
        if (report is null)
        {
            return new PlayerReportAccessResult(PlayerReportAccessStatus.NotFound);
        }

        if (report.ReporterUserId != actor.Id)
        {
            return new PlayerReportAccessResult(PlayerReportAccessStatus.Forbidden);
        }

        return new PlayerReportAccessResult(PlayerReportAccessStatus.Success, actor, report);
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

    private static bool HasModeratorAccess(IEnumerable<Claim> claims) =>
        claims.Any(claim =>
            claim.Type == ClaimTypes.Role &&
            (string.Equals(claim.Value, "moderator", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(claim.Value, "admin", StringComparison.OrdinalIgnoreCase)));

    private static bool CanManageTitleReports(string? role) =>
        string.Equals(role, "owner", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "editor", StringComparison.OrdinalIgnoreCase);

    private static string GetActorDisplayName(AppUser actor) =>
        actor.DisplayName
        ?? actor.UserName
        ?? actor.Email
        ?? "A developer";

    private static string BuildModeratorReportActionUrl(Guid reportId) =>
        $"/moderate?workflow=reports-review&reportId={Uri.EscapeDataString(reportId.ToString("D"))}";

    private static string BuildDeveloperReportActionUrl(Guid titleId, Guid reportId) =>
        $"/develop?domain=titles&workflow=titles-reports&titleId={Uri.EscapeDataString(titleId.ToString("D"))}&reportId={Uri.EscapeDataString(reportId.ToString("D"))}";

    private static string BuildPlayerReportActionUrl(Guid reportId) =>
        $"/player?workflow=reported-titles&reportId={Uri.EscapeDataString(reportId.ToString("D"))}";

    private static string? NormalizeModeratorRecipientRole(string? recipientRole) =>
        recipientRole?.Trim().ToLowerInvariant() switch
        {
            TitleReportAuthorRoles.Player => TitleReportAuthorRoles.Player,
            TitleReportAuthorRoles.Developer => TitleReportAuthorRoles.Developer,
            _ => null
        };

    private static TitleReportSummarySnapshot MapSummary(TitleReport report) =>
        new(
            report.Id,
            report.TitleId,
            report.Title.StudioId,
            report.Title.Studio.Slug,
            report.Title.Studio.DisplayName,
            report.Title.Slug,
            report.Title.CurrentMetadataVersion!.DisplayName,
            report.Title.CurrentMetadataVersion.ShortDescription,
            report.Title.CurrentMetadataVersion.GenreDisplay,
            report.Title.CurrentMetadataVersion.RevisionNumber,
            report.ReporterUser.KeycloakSubject,
            report.ReporterUser.UserName,
            report.ReporterUser.DisplayName,
            report.ReporterUser.Email,
            report.Status,
            report.Reason,
            report.CreatedAtUtc,
            report.UpdatedAtUtc,
            report.ResolvedAtUtc,
            report.Messages.Count);

    private static TitleReportDetailSnapshot MapDetail(TitleReport report, string viewerRole) =>
        new(
            MapSummary(report),
            report.ResolutionNote,
            report.ResolvedByUser is null
                ? null
                : new TitleReportActorSnapshot(
                    report.ResolvedByUser.KeycloakSubject,
                    report.ResolvedByUser.UserName,
                    report.ResolvedByUser.DisplayName,
                    report.ResolvedByUser.Email),
            report.Messages
                .OrderBy(message => message.CreatedAtUtc)
                .Where(message => CanViewerSeeMessage(message, viewerRole))
                .Select(message => new TitleReportMessageSnapshot(
                    message.Id,
                    message.AuthorUser.KeycloakSubject,
                    message.AuthorUser.UserName,
                    message.AuthorUser.DisplayName,
                    message.AuthorUser.Email,
                    message.AuthorRole,
                    message.Audience,
                    message.Message,
                    message.CreatedAtUtc))
                .ToArray());

    private static bool CanViewerSeeMessage(TitleReportMessage message, string viewerRole) =>
        viewerRole == TitleReportViewerRoles.Moderator ||
        message.Audience == TitleReportAudiences.All ||
        (viewerRole == TitleReportViewerRoles.Developer && message.Audience == TitleReportAudiences.Developer) ||
        (viewerRole == TitleReportViewerRoles.Player && message.Audience == TitleReportAudiences.Player);
}

/// <summary>
/// Title-report summary returned by moderation and developer list endpoints.
/// </summary>
internal sealed record TitleReportSummarySnapshot(
    Guid Id,
    Guid TitleId,
    Guid StudioId,
    string StudioSlug,
    string StudioDisplayName,
    string TitleSlug,
    string TitleDisplayName,
    string TitleShortDescription,
    string GenreDisplay,
    int CurrentMetadataRevision,
    string ReporterSubject,
    string? ReporterUserName,
    string? ReporterDisplayName,
    string? ReporterEmail,
    string Status,
    string Reason,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ResolvedAtUtc,
    int MessageCount);

/// <summary>
/// Detailed title-report thread payload.
/// </summary>
internal sealed record TitleReportDetailSnapshot(
    TitleReportSummarySnapshot Report,
    string? ResolutionNote,
    TitleReportActorSnapshot? ResolvedBy,
    IReadOnlyList<TitleReportMessageSnapshot> Messages);

/// <summary>
/// Lightweight actor summary rendered in title-report workflows.
/// </summary>
internal sealed record TitleReportActorSnapshot(
    string Subject,
    string? UserName,
    string? DisplayName,
    string? Email);

/// <summary>
/// Thread message snapshot for title-report discussions.
/// </summary>
internal sealed record TitleReportMessageSnapshot(
    Guid Id,
    string AuthorSubject,
    string? AuthorUserName,
    string? AuthorDisplayName,
    string? AuthorEmail,
    string AuthorRole,
    string Audience,
    string Message,
    DateTime CreatedAtUtc);

/// <summary>
/// Result wrapper for title-report list operations.
/// </summary>
internal sealed record TitleReportListResult(TitleReportListStatus Status, IReadOnlyList<TitleReportSummarySnapshot>? Reports = null);

/// <summary>
/// Result wrapper for title-report detail and mutation operations.
/// </summary>
internal sealed record TitleReportMutationResult(TitleReportMutationStatus Status, TitleReportDetailSnapshot? Report = null);

/// <summary>
/// Title-report list status codes.
/// </summary>
internal enum TitleReportListStatus
{
    Success,
    NotFound,
    Forbidden
}

/// <summary>
/// Title-report mutation status codes.
/// </summary>
internal enum TitleReportMutationStatus
{
    Success,
    NotFound,
    Forbidden,
    ValidationFailed
}

/// <summary>
/// Developer-title access status used by report workflows.
/// </summary>
internal enum DeveloperTitleAccessStatus
{
    Success,
    NotFound,
    Forbidden
}

/// <summary>
/// Developer-title access result for report workflows.
/// </summary>
internal sealed record DeveloperTitleAccessResult(DeveloperTitleAccessStatus Status, AppUser? Actor = null);

/// <summary>
/// Player-report access status used by player report workflows.
/// </summary>
internal enum PlayerReportAccessStatus
{
    Success,
    NotFound,
    Forbidden
}

/// <summary>
/// Player-report access result for report workflows.
/// </summary>
internal sealed record PlayerReportAccessResult(PlayerReportAccessStatus Status, AppUser? Actor = null, TitleReport? Report = null);

/// <summary>
/// Participant viewer roles used to filter report-thread messages.
/// </summary>
internal static class TitleReportViewerRoles
{
    public const string Moderator = "moderator";
    public const string Developer = "developer";
    public const string Player = "player";
}

/// <summary>
/// Message-audience lanes used to segment title-report threads.
/// </summary>
internal static class TitleReportAudiences
{
    public const string All = "all";
    public const string Developer = "developer";
    public const string Player = "player";
}
