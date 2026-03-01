using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Identity;
using Board.ThirdPartyLibrary.Api.Organizations;
using Board.ThirdPartyLibrary.Api.Persistence;
using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Board.ThirdPartyLibrary.Api.Titles;

internal interface ITitleService
{
    Task<IReadOnlyList<TitleSnapshot>> ListPublicTitlesAsync(
        string? organizationSlug,
        string? contentKind,
        CancellationToken cancellationToken = default);

    Task<TitleSnapshot?> GetPublicTitleAsync(
        string organizationSlug,
        string titleSlug,
        CancellationToken cancellationToken = default);

    Task<TitleListResult> ListOrganizationTitlesAsync(
        IEnumerable<Claim> claims,
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<TitleMutationResult> CreateTitleAsync(
        IEnumerable<Claim> claims,
        Guid organizationId,
        CreateTitleCommand command,
        CancellationToken cancellationToken = default);

    Task<TitleMutationResult> GetTitleAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        CancellationToken cancellationToken = default);

    Task<TitleMutationResult> UpdateTitleAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        UpdateTitleCommand command,
        CancellationToken cancellationToken = default);

    Task<TitleMutationResult> UpsertCurrentMetadataAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        UpsertTitleMetadataCommand command,
        CancellationToken cancellationToken = default);

    Task<TitleMetadataVersionListResult> ListMetadataVersionsAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        CancellationToken cancellationToken = default);

    Task<TitleMutationResult> ActivateMetadataVersionAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        int revisionNumber,
        CancellationToken cancellationToken = default);
}

internal sealed class TitleService(
    BoardLibraryDbContext dbContext,
    IIdentityPersistenceService identityPersistenceService) : ITitleService
{
    public async Task<IReadOnlyList<TitleSnapshot>> ListPublicTitlesAsync(
        string? organizationSlug,
        string? contentKind,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Titles
            .AsNoTracking()
            .Include(candidate => candidate.Organization)
            .Include(candidate => candidate.CurrentMetadataVersion)
            .Where(candidate =>
                candidate.CurrentMetadataVersionId != null &&
                candidate.CurrentMetadataVersion != null &&
                candidate.Visibility == TitleVisibilities.Listed &&
                (candidate.LifecycleStatus == TitleLifecycleStatuses.Testing ||
                 candidate.LifecycleStatus == TitleLifecycleStatuses.Published));

        if (!string.IsNullOrWhiteSpace(organizationSlug))
        {
            query = query.Where(candidate => candidate.Organization.Slug == organizationSlug);
        }

        if (!string.IsNullOrWhiteSpace(contentKind))
        {
            query = query.Where(candidate => candidate.ContentKind == contentKind);
        }

        return await query
            .OrderBy(candidate => candidate.Organization.DisplayName)
            .ThenBy(candidate => candidate.CurrentMetadataVersion!.DisplayName)
            .Select(candidate => MapTitle(candidate, includeDescription: false))
            .ToListAsync(cancellationToken);
    }

    public async Task<TitleSnapshot?> GetPublicTitleAsync(
        string organizationSlug,
        string titleSlug,
        CancellationToken cancellationToken = default)
    {
        var title = await dbContext.Titles
            .AsNoTracking()
            .Include(candidate => candidate.Organization)
            .Include(candidate => candidate.CurrentMetadataVersion)
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Organization.Slug == organizationSlug &&
                    candidate.Slug == titleSlug &&
                    candidate.CurrentMetadataVersionId != null &&
                    candidate.CurrentMetadataVersion != null &&
                    candidate.Visibility != TitleVisibilities.Private &&
                    (candidate.LifecycleStatus == TitleLifecycleStatuses.Testing ||
                     candidate.LifecycleStatus == TitleLifecycleStatuses.Published),
                cancellationToken);

        return title is null ? null : MapTitle(title, includeDescription: true);
    }

    public async Task<TitleListResult> ListOrganizationTitlesAsync(
        IEnumerable<Claim> claims,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var organizationExists = await dbContext.Organizations
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Id == organizationId, cancellationToken);

        if (!organizationExists)
        {
            return new TitleListResult(TitleListStatus.NotFound);
        }

        var actorRole = await GetActorOrganizationRoleAsync(organizationId, actor.Id, cancellationToken);
        if (!CanManageTitles(actorRole))
        {
            return new TitleListResult(TitleListStatus.Forbidden);
        }

        var titles = await dbContext.Titles
            .AsNoTracking()
            .Include(candidate => candidate.Organization)
            .Include(candidate => candidate.CurrentMetadataVersion)
            .Where(candidate => candidate.OrganizationId == organizationId && candidate.CurrentMetadataVersionId != null)
            .OrderBy(candidate => candidate.CurrentMetadataVersion!.DisplayName)
            .Select(candidate => MapTitle(candidate, includeDescription: false))
            .ToListAsync(cancellationToken);

        return new TitleListResult(TitleListStatus.Success, titles);
    }

    public async Task<TitleMutationResult> CreateTitleAsync(
        IEnumerable<Claim> claims,
        Guid organizationId,
        CreateTitleCommand command,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == organizationId, cancellationToken);

        if (organization is null)
        {
            return new TitleMutationResult(TitleMutationStatus.NotFound);
        }

        var actorRole = await GetActorOrganizationRoleAsync(organizationId, actor.Id, cancellationToken);
        if (!CanManageTitles(actorRole))
        {
            return new TitleMutationResult(TitleMutationStatus.Forbidden);
        }

        var now = DateTime.UtcNow;
        var title = new Title
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Slug = command.Slug,
            ContentKind = command.ContentKind,
            LifecycleStatus = command.LifecycleStatus,
            Visibility = command.Visibility,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var metadataVersion = new TitleMetadataVersion
        {
            Id = Guid.NewGuid(),
            TitleId = title.Id,
            RevisionNumber = 1,
            DisplayName = command.Metadata.DisplayName,
            ShortDescription = command.Metadata.ShortDescription,
            Description = command.Metadata.Description,
            GenreDisplay = command.Metadata.GenreDisplay,
            MinPlayers = command.Metadata.MinPlayers,
            MaxPlayers = command.Metadata.MaxPlayers,
            AgeRatingAuthority = command.Metadata.AgeRatingAuthority,
            AgeRatingValue = command.Metadata.AgeRatingValue,
            MinAgeYears = command.Metadata.MinAgeYears,
            IsFrozen = command.LifecycleStatus != TitleLifecycleStatuses.Draft,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Titles.Add(title);
        dbContext.TitleMetadataVersions.Add(metadataVersion);

        try
        {
            await ExecuteInOptionalTransactionAsync(
                async () =>
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    title.CurrentMetadataVersionId = metadataVersion.Id;
                    title.UpdatedAtUtc = now;
                    await dbContext.SaveChangesAsync(cancellationToken);
                },
                cancellationToken);
        }
        catch (DbUpdateException)
        {
            return new TitleMutationResult(TitleMutationStatus.Conflict);
        }

        return new TitleMutationResult(
            TitleMutationStatus.Success,
            MapTitle(title, organization, metadataVersion, includeDescription: true));
    }

    public async Task<TitleMutationResult> GetTitleAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        CancellationToken cancellationToken = default)
    {
        var title = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        return title.Status switch
        {
            TitleAccessStatus.Success => new TitleMutationResult(
                TitleMutationStatus.Success,
                MapTitle(title.Title!, includeDescription: true)),
            TitleAccessStatus.NotFound => new TitleMutationResult(TitleMutationStatus.NotFound),
            TitleAccessStatus.Forbidden => new TitleMutationResult(TitleMutationStatus.Forbidden),
            _ => new TitleMutationResult(TitleMutationStatus.NotFound)
        };
    }

    public async Task<TitleMutationResult> UpdateTitleAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        UpdateTitleCommand command,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not TitleAccessStatus.Success)
        {
            return access.Status == TitleAccessStatus.Forbidden
                ? new TitleMutationResult(TitleMutationStatus.Forbidden)
                : new TitleMutationResult(TitleMutationStatus.NotFound);
        }

        var title = access.Title!;
        var now = DateTime.UtcNow;
        var isLeavingDraft =
            title.LifecycleStatus == TitleLifecycleStatuses.Draft &&
            command.LifecycleStatus != TitleLifecycleStatuses.Draft;

        title.Slug = command.Slug;
        title.ContentKind = command.ContentKind;
        title.LifecycleStatus = command.LifecycleStatus;
        title.Visibility = command.Visibility;
        title.UpdatedAtUtc = now;

        if (isLeavingDraft && title.CurrentMetadataVersion is not null && !title.CurrentMetadataVersion.IsFrozen)
        {
            title.CurrentMetadataVersion.IsFrozen = true;
            title.CurrentMetadataVersion.UpdatedAtUtc = now;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new TitleMutationResult(TitleMutationStatus.Success, MapTitle(title, includeDescription: true));
        }
        catch (DbUpdateException)
        {
            return new TitleMutationResult(TitleMutationStatus.Conflict);
        }
    }

    public async Task<TitleMutationResult> UpsertCurrentMetadataAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        UpsertTitleMetadataCommand command,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not TitleAccessStatus.Success)
        {
            return access.Status == TitleAccessStatus.Forbidden
                ? new TitleMutationResult(TitleMutationStatus.Forbidden)
                : new TitleMutationResult(TitleMutationStatus.NotFound);
        }

        var title = access.Title!;
        var now = DateTime.UtcNow;

        if (title.CurrentMetadataVersion is not null &&
            title.LifecycleStatus == TitleLifecycleStatuses.Draft &&
            !title.CurrentMetadataVersion.IsFrozen)
        {
            ApplyMetadata(title.CurrentMetadataVersion, command, now);
            title.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new TitleMutationResult(TitleMutationStatus.Success, MapTitle(title, includeDescription: true));
        }

        var nextRevision = await dbContext.TitleMetadataVersions
            .Where(candidate => candidate.TitleId == title.Id)
            .Select(candidate => (int?)candidate.RevisionNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var newMetadataVersion = new TitleMetadataVersion
        {
            Id = Guid.NewGuid(),
            TitleId = title.Id,
            RevisionNumber = nextRevision + 1,
            IsFrozen = title.LifecycleStatus != TitleLifecycleStatuses.Draft,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        ApplyMetadata(newMetadataVersion, command, now);

        dbContext.TitleMetadataVersions.Add(newMetadataVersion);

        await ExecuteInOptionalTransactionAsync(
            async () =>
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                title.CurrentMetadataVersionId = newMetadataVersion.Id;
                title.UpdatedAtUtc = now;
                await dbContext.SaveChangesAsync(cancellationToken);
            },
            cancellationToken);

        return new TitleMutationResult(TitleMutationStatus.Success, MapTitle(title, includeDescription: true));
    }

    public async Task<TitleMetadataVersionListResult> ListMetadataVersionsAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not TitleAccessStatus.Success)
        {
            return access.Status == TitleAccessStatus.Forbidden
                ? new TitleMetadataVersionListResult(TitleMetadataVersionListStatus.Forbidden)
                : new TitleMetadataVersionListResult(TitleMetadataVersionListStatus.NotFound);
        }

        var versions = await dbContext.TitleMetadataVersions
            .AsNoTracking()
            .Where(candidate => candidate.TitleId == titleId)
            .OrderBy(candidate => candidate.RevisionNumber)
            .Select(candidate => new TitleMetadataVersionSnapshot(
                candidate.RevisionNumber,
                candidate.Id == access.Title!.CurrentMetadataVersionId,
                candidate.IsFrozen,
                candidate.DisplayName,
                candidate.ShortDescription,
                candidate.Description,
                candidate.GenreDisplay,
                candidate.MinPlayers,
                candidate.MaxPlayers,
                candidate.AgeRatingAuthority,
                candidate.AgeRatingValue,
                candidate.MinAgeYears,
                candidate.CreatedAtUtc,
                candidate.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new TitleMetadataVersionListResult(TitleMetadataVersionListStatus.Success, versions);
    }

    public async Task<TitleMutationResult> ActivateMetadataVersionAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        int revisionNumber,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not TitleAccessStatus.Success)
        {
            return access.Status == TitleAccessStatus.Forbidden
                ? new TitleMutationResult(TitleMutationStatus.Forbidden)
                : new TitleMutationResult(TitleMutationStatus.NotFound);
        }

        var title = access.Title!;
        var metadataVersion = await dbContext.TitleMetadataVersions
            .SingleOrDefaultAsync(
                candidate => candidate.TitleId == titleId && candidate.RevisionNumber == revisionNumber,
                cancellationToken);

        if (metadataVersion is null)
        {
            return new TitleMutationResult(TitleMutationStatus.NotFound);
        }

        var now = DateTime.UtcNow;
        title.CurrentMetadataVersionId = metadataVersion.Id;
        title.UpdatedAtUtc = now;

        if (title.LifecycleStatus != TitleLifecycleStatuses.Draft && !metadataVersion.IsFrozen)
        {
            metadataVersion.IsFrozen = true;
            metadataVersion.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new TitleMutationResult(TitleMutationStatus.Success, MapTitle(title, includeDescription: true));
    }

    private async Task<TitleAccessResult> LoadManagedTitleAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        CancellationToken cancellationToken)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var title = await dbContext.Titles
            .Include(candidate => candidate.Organization)
            .Include(candidate => candidate.CurrentMetadataVersion)
            .SingleOrDefaultAsync(candidate => candidate.Id == titleId, cancellationToken);

        if (title is null)
        {
            return new TitleAccessResult(TitleAccessStatus.NotFound);
        }

        var actorRole = await GetActorOrganizationRoleAsync(title.OrganizationId, actor.Id, cancellationToken);
        if (!CanManageTitles(actorRole))
        {
            return new TitleAccessResult(TitleAccessStatus.Forbidden);
        }

        return new TitleAccessResult(TitleAccessStatus.Success, title);
    }

    private async Task<AppUser> EnsureActorAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        await identityPersistenceService.EnsureCurrentUserProjectionAsync(claims, cancellationToken);
        var subject = GetRequiredSubject(claims);

        return await dbContext.Users.SingleAsync(candidate => candidate.KeycloakSubject == subject, cancellationToken);
    }

    private async Task<string?> GetActorOrganizationRoleAsync(Guid organizationId, Guid actorUserId, CancellationToken cancellationToken) =>
        await dbContext.OrganizationMemberships
            .Where(candidate => candidate.OrganizationId == organizationId && candidate.UserId == actorUserId)
            .Select(candidate => candidate.Role)
            .SingleOrDefaultAsync(cancellationToken);

    private static bool CanManageTitles(string? role) =>
        string.Equals(role, OrganizationRoles.Owner, StringComparison.Ordinal) ||
        string.Equals(role, OrganizationRoles.Admin, StringComparison.Ordinal) ||
        string.Equals(role, OrganizationRoles.Editor, StringComparison.Ordinal);

    private static void ApplyMetadata(TitleMetadataVersion metadataVersion, UpsertTitleMetadataCommand command, DateTime now)
    {
        metadataVersion.DisplayName = command.DisplayName;
        metadataVersion.ShortDescription = command.ShortDescription;
        metadataVersion.Description = command.Description;
        metadataVersion.GenreDisplay = command.GenreDisplay;
        metadataVersion.MinPlayers = command.MinPlayers;
        metadataVersion.MaxPlayers = command.MaxPlayers;
        metadataVersion.AgeRatingAuthority = command.AgeRatingAuthority;
        metadataVersion.AgeRatingValue = command.AgeRatingValue;
        metadataVersion.MinAgeYears = command.MinAgeYears;
        metadataVersion.UpdatedAtUtc = now;
    }

    private static TitleSnapshot MapTitle(Title title, bool includeDescription) =>
        MapTitle(title, title.Organization, title.CurrentMetadataVersion!, includeDescription);

    private static TitleSnapshot MapTitle(
        Title title,
        Organization organization,
        TitleMetadataVersion metadataVersion,
        bool includeDescription) =>
        new(
            title.Id,
            title.OrganizationId,
            organization.Slug,
            title.Slug,
            title.ContentKind,
            title.LifecycleStatus,
            title.Visibility,
            metadataVersion.RevisionNumber,
            metadataVersion.DisplayName,
            metadataVersion.ShortDescription,
            includeDescription ? metadataVersion.Description : null,
            metadataVersion.GenreDisplay,
            metadataVersion.MinPlayers,
            metadataVersion.MaxPlayers,
            metadataVersion.AgeRatingAuthority,
            metadataVersion.AgeRatingValue,
            metadataVersion.MinAgeYears,
            includeDescription ? title.CreatedAtUtc : null,
            includeDescription ? title.UpdatedAtUtc : null);

    private static string GetRequiredSubject(IEnumerable<Claim> claims)
    {
        var subject = claims.FirstOrDefault(claim => string.Equals(claim.Type, "sub", StringComparison.OrdinalIgnoreCase))?.Value;

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new InvalidOperationException("Authenticated user is missing the required subject claim.");
        }

        return subject;
    }

    private async Task ExecuteInOptionalTransactionAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        if (string.Equals(dbContext.Database.ProviderName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.Ordinal))
        {
            await action();
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await action();
        await transaction.CommitAsync(cancellationToken);
    }
}

internal sealed record CreateTitleCommand(
    string Slug,
    string ContentKind,
    string LifecycleStatus,
    string Visibility,
    UpsertTitleMetadataCommand Metadata);

internal sealed record UpdateTitleCommand(
    string Slug,
    string ContentKind,
    string LifecycleStatus,
    string Visibility);

internal sealed record UpsertTitleMetadataCommand(
    string DisplayName,
    string ShortDescription,
    string Description,
    string GenreDisplay,
    int MinPlayers,
    int MaxPlayers,
    string AgeRatingAuthority,
    string AgeRatingValue,
    int MinAgeYears);

internal sealed record TitleSnapshot(
    Guid Id,
    Guid OrganizationId,
    string OrganizationSlug,
    string Slug,
    string ContentKind,
    string LifecycleStatus,
    string Visibility,
    int CurrentMetadataRevision,
    string DisplayName,
    string ShortDescription,
    string? Description,
    string GenreDisplay,
    int MinPlayers,
    int MaxPlayers,
    string AgeRatingAuthority,
    string AgeRatingValue,
    int MinAgeYears,
    DateTime? CreatedAtUtc,
    DateTime? UpdatedAtUtc);

internal sealed record TitleMetadataVersionSnapshot(
    int RevisionNumber,
    bool IsCurrent,
    bool IsFrozen,
    string DisplayName,
    string ShortDescription,
    string Description,
    string GenreDisplay,
    int MinPlayers,
    int MaxPlayers,
    string AgeRatingAuthority,
    string AgeRatingValue,
    int MinAgeYears,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

internal sealed record TitleMutationResult(
    TitleMutationStatus Status,
    TitleSnapshot? Title = null);

internal sealed record TitleListResult(
    TitleListStatus Status,
    IReadOnlyList<TitleSnapshot>? Titles = null);

internal sealed record TitleMetadataVersionListResult(
    TitleMetadataVersionListStatus Status,
    IReadOnlyList<TitleMetadataVersionSnapshot>? MetadataVersions = null);

internal sealed record TitleAccessResult(
    TitleAccessStatus Status,
    Title? Title = null);

internal enum TitleMutationStatus
{
    Success,
    NotFound,
    Forbidden,
    Conflict
}

internal enum TitleListStatus
{
    Success,
    NotFound,
    Forbidden
}

internal enum TitleMetadataVersionListStatus
{
    Success,
    NotFound,
    Forbidden
}

internal enum TitleAccessStatus
{
    Success,
    NotFound,
    Forbidden
}

internal static class TitleLifecycleStatuses
{
    public const string Draft = "draft";
    public const string Testing = "testing";
    public const string Published = "published";
    public const string Archived = "archived";
}

internal static class TitleVisibilities
{
    public const string Private = "private";
    public const string Unlisted = "unlisted";
    public const string Listed = "listed";
}

internal static class TitleContentKinds
{
    public const string Game = "game";
    public const string App = "app";
}
