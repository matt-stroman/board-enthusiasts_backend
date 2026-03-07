using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Auth;
using Board.ThirdPartyLibrary.Api.Identity;
using Board.ThirdPartyLibrary.Api.Persistence;
using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Board.ThirdPartyLibrary.Api.Studios;

internal interface IStudioService
{
    Task<IReadOnlyList<StudioSummarySnapshot>> ListStudiosAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeveloperStudioSummarySnapshot>> ListManagedStudiosAsync(
        IEnumerable<Claim> claims,
        CancellationToken cancellationToken = default);

    Task<StudioSnapshot?> GetStudioBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<StudioMutationResult> CreateStudioAsync(
        IEnumerable<Claim> claims,
        CreateStudioCommand command,
        CancellationToken cancellationToken = default);

    Task<StudioMutationResult> UpdateStudioAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        UpdateStudioCommand command,
        CancellationToken cancellationToken = default);

    Task<StudioMutationResult> SetStudioMediaUrlAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        string mediaRole,
        string sourceUrl,
        CancellationToken cancellationToken = default);

    Task<StudioDeleteStatus> DeleteStudioAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        CancellationToken cancellationToken = default);

    Task<StudioLinkListResult> ListLinksAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        CancellationToken cancellationToken = default);

    Task<StudioLinkMutationResult> CreateLinkAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        UpsertStudioLinkCommand command,
        CancellationToken cancellationToken = default);

    Task<StudioLinkMutationResult> UpdateLinkAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        Guid linkId,
        UpsertStudioLinkCommand command,
        CancellationToken cancellationToken = default);

    Task<StudioLinkDeleteStatus> DeleteLinkAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        Guid linkId,
        CancellationToken cancellationToken = default);

    Task<StudioMembershipListResult> ListMembershipsAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        CancellationToken cancellationToken = default);

    Task<StudioMembershipMutationResult> UpsertMembershipAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        UpsertStudioMembershipCommand command,
        CancellationToken cancellationToken = default);

    Task<StudioMembershipDeleteStatus> DeleteMembershipAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        string memberKeycloakSubject,
        CancellationToken cancellationToken = default);
}

internal sealed class StudioService(
    BoardLibraryDbContext dbContext,
    IIdentityPersistenceService identityPersistenceService) : IStudioService
{
    public async Task<IReadOnlyList<StudioSummarySnapshot>> ListStudiosAsync(CancellationToken cancellationToken = default)
    {
        var studios = await dbContext.Studios
            .AsNoTracking()
            .Include(candidate => candidate.Links)
            .OrderBy(candidate => candidate.DisplayName)
            .ToListAsync(cancellationToken);

        return studios.Select(MapStudioSummary).ToList();
    }

    public async Task<IReadOnlyList<DeveloperStudioSummarySnapshot>> ListManagedStudiosAsync(
        IEnumerable<Claim> claims,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);

        var memberships = await dbContext.StudioMemberships
            .AsNoTracking()
            .Where(candidate =>
                candidate.UserId == actor.Id &&
                (candidate.Role == StudioRoles.Owner ||
                 candidate.Role == StudioRoles.Admin ||
                 candidate.Role == StudioRoles.Editor))
            .Include(candidate => candidate.Studio)
                .ThenInclude(candidate => candidate.Links)
            .OrderBy(candidate => candidate.Studio.DisplayName)
            .ToListAsync(cancellationToken);

        return memberships
            .Select(candidate => new DeveloperStudioSummarySnapshot(
                candidate.StudioId,
                candidate.Studio.Slug,
                candidate.Studio.DisplayName,
                candidate.Studio.Description,
                candidate.Studio.LogoUrl,
                candidate.Studio.BannerUrl,
                candidate.Studio.Links
                    .OrderBy(link => link.Label)
                    .ThenBy(link => link.Url)
                    .Select(MapStudioLink)
                    .ToList(),
                candidate.Role))
            .ToList();
    }

    public async Task<StudioSnapshot?> GetStudioBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var studio = await dbContext.Studios
            .AsNoTracking()
            .Include(candidate => candidate.Links)
            .SingleOrDefaultAsync(candidate => candidate.Slug == slug, cancellationToken);

        return studio is null ? null : MapStudio(studio);
    }

    public async Task<StudioMutationResult> CreateStudioAsync(
        IEnumerable<Claim> claims,
        CreateStudioCommand command,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var now = DateTime.UtcNow;
        var studio = new Studio
        {
            Id = Guid.NewGuid(),
            Slug = command.Slug,
            DisplayName = command.DisplayName,
            Description = command.Description,
            LogoUrl = command.LogoUrl,
            BannerUrl = command.BannerUrl,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var membership = new StudioMembership
        {
            StudioId = studio.Id,
            UserId = actor.Id,
            Role = StudioRoles.Owner,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Studios.Add(studio);
        dbContext.StudioMemberships.Add(membership);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new StudioMutationResult(StudioMutationStatus.Success, MapStudio(studio));
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(studio).State = EntityState.Detached;
            dbContext.Entry(membership).State = EntityState.Detached;
            return new StudioMutationResult(StudioMutationStatus.Conflict);
        }
    }

    public async Task<StudioMutationResult> UpdateStudioAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        UpdateStudioCommand command,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var studio = await dbContext.Studios
            .SingleOrDefaultAsync(candidate => candidate.Id == studioId, cancellationToken);

        if (studio is null)
        {
            return new StudioMutationResult(StudioMutationStatus.NotFound);
        }

        var actorRole = await GetActorStudioRoleAsync(studioId, actor.Id, cancellationToken);
        if (!CanManageStudio(actorRole))
        {
            return new StudioMutationResult(StudioMutationStatus.Forbidden);
        }

        studio.Slug = command.Slug;
        studio.DisplayName = command.DisplayName;
        studio.Description = command.Description;
        studio.LogoUrl = command.LogoUrl;
        studio.BannerUrl = command.BannerUrl;
        studio.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new StudioMutationResult(StudioMutationStatus.Success, MapStudio(studio));
        }
        catch (DbUpdateException)
        {
            return new StudioMutationResult(StudioMutationStatus.Conflict);
        }
    }

    public async Task<StudioMutationResult> SetStudioMediaUrlAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        string mediaRole,
        string sourceUrl,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var studio = await dbContext.Studios
            .Include(candidate => candidate.Links)
            .SingleOrDefaultAsync(candidate => candidate.Id == studioId, cancellationToken);

        if (studio is null)
        {
            return new StudioMutationResult(StudioMutationStatus.NotFound);
        }

        var actorRole = await GetActorStudioRoleAsync(studioId, actor.Id, cancellationToken);
        if (!CanManageStudio(actorRole))
        {
            return new StudioMutationResult(StudioMutationStatus.Forbidden);
        }

        if (string.Equals(mediaRole, StudioMediaRoles.Logo, StringComparison.Ordinal))
        {
            studio.LogoUrl = sourceUrl;
        }
        else if (string.Equals(mediaRole, StudioMediaRoles.Banner, StringComparison.Ordinal))
        {
            studio.BannerUrl = sourceUrl;
        }
        else
        {
            return new StudioMutationResult(StudioMutationStatus.NotFound);
        }

        studio.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new StudioMutationResult(StudioMutationStatus.Success, MapStudio(studio));
    }

    public async Task<StudioDeleteStatus> DeleteStudioAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var studio = await dbContext.Studios
            .SingleOrDefaultAsync(candidate => candidate.Id == studioId, cancellationToken);

        if (studio is null)
        {
            return StudioDeleteStatus.NotFound;
        }

        var actorRole = await GetActorStudioRoleAsync(studioId, actor.Id, cancellationToken);
        if (!string.Equals(actorRole, StudioRoles.Owner, StringComparison.Ordinal))
        {
            return StudioDeleteStatus.Forbidden;
        }

        var memberships = await dbContext.StudioMemberships
            .Where(candidate => candidate.StudioId == studioId)
            .ToListAsync(cancellationToken);

        var links = await dbContext.StudioLinks
            .Where(candidate => candidate.StudioId == studioId)
            .ToListAsync(cancellationToken);

        dbContext.StudioLinks.RemoveRange(links);
        dbContext.StudioMemberships.RemoveRange(memberships);
        dbContext.Studios.Remove(studio);
        await dbContext.SaveChangesAsync(cancellationToken);
        return StudioDeleteStatus.Success;
    }

    public async Task<StudioLinkListResult> ListLinksAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var studioExists = await dbContext.Studios
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Id == studioId, cancellationToken);

        if (!studioExists)
        {
            return new StudioLinkListResult(StudioLinkListStatus.NotFound);
        }

        var actorRole = await GetActorStudioRoleAsync(studioId, actor.Id, cancellationToken);
        if (!CanManageStudio(actorRole))
        {
            return new StudioLinkListResult(StudioLinkListStatus.Forbidden);
        }

        var links = await dbContext.StudioLinks
            .AsNoTracking()
            .Where(candidate => candidate.StudioId == studioId)
            .OrderBy(candidate => candidate.Label)
            .ThenBy(candidate => candidate.Url)
            .Select(candidate => MapStudioLink(candidate))
            .ToListAsync(cancellationToken);

        return new StudioLinkListResult(StudioLinkListStatus.Success, links);
    }

    public async Task<StudioLinkMutationResult> CreateLinkAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        UpsertStudioLinkCommand command,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedStudioAsync(claims, studioId, cancellationToken);
        if (access.Status is not StudioAccessStatus.Success)
        {
            return access.Status == StudioAccessStatus.Forbidden
                ? new StudioLinkMutationResult(StudioLinkMutationStatus.Forbidden)
                : new StudioLinkMutationResult(StudioLinkMutationStatus.NotFound);
        }

        var now = DateTime.UtcNow;
        var link = new StudioLink
        {
            Id = Guid.NewGuid(),
            StudioId = studioId,
            Label = command.Label,
            Url = command.Url,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.StudioLinks.Add(link);
        access.Studio!.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new StudioLinkMutationResult(StudioLinkMutationStatus.Success, MapStudioLink(link));
    }

    public async Task<StudioLinkMutationResult> UpdateLinkAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        Guid linkId,
        UpsertStudioLinkCommand command,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedStudioAsync(claims, studioId, cancellationToken);
        if (access.Status is not StudioAccessStatus.Success)
        {
            return access.Status == StudioAccessStatus.Forbidden
                ? new StudioLinkMutationResult(StudioLinkMutationStatus.Forbidden)
                : new StudioLinkMutationResult(StudioLinkMutationStatus.NotFound);
        }

        var link = await dbContext.StudioLinks
            .SingleOrDefaultAsync(candidate => candidate.StudioId == studioId && candidate.Id == linkId, cancellationToken);
        if (link is null)
        {
            return new StudioLinkMutationResult(StudioLinkMutationStatus.NotFound);
        }

        link.Label = command.Label;
        link.Url = command.Url;
        link.UpdatedAtUtc = DateTime.UtcNow;
        access.Studio!.UpdatedAtUtc = link.UpdatedAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new StudioLinkMutationResult(StudioLinkMutationStatus.Success, MapStudioLink(link));
    }

    public async Task<StudioLinkDeleteStatus> DeleteLinkAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        Guid linkId,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedStudioAsync(claims, studioId, cancellationToken);
        if (access.Status is not StudioAccessStatus.Success)
        {
            return access.Status == StudioAccessStatus.Forbidden
                ? StudioLinkDeleteStatus.Forbidden
                : StudioLinkDeleteStatus.NotFound;
        }

        var link = await dbContext.StudioLinks
            .SingleOrDefaultAsync(candidate => candidate.StudioId == studioId && candidate.Id == linkId, cancellationToken);
        if (link is null)
        {
            return StudioLinkDeleteStatus.NotFound;
        }

        dbContext.StudioLinks.Remove(link);
        access.Studio!.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return StudioLinkDeleteStatus.Success;
    }

    public async Task<StudioMembershipListResult> ListMembershipsAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var studioExists = await dbContext.Studios
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Id == studioId, cancellationToken);

        if (!studioExists)
        {
            return new StudioMembershipListResult(StudioMembershipListStatus.NotFound);
        }

        var actorRole = await GetActorStudioRoleAsync(studioId, actor.Id, cancellationToken);
        if (!CanManageMemberships(actorRole))
        {
            return new StudioMembershipListResult(StudioMembershipListStatus.Forbidden);
        }

        var memberships = await dbContext.StudioMemberships
            .AsNoTracking()
            .Where(candidate => candidate.StudioId == studioId)
            .Include(candidate => candidate.User)
            .OrderBy(candidate => candidate.Role)
            .ThenBy(candidate => candidate.User.DisplayName)
            .ThenBy(candidate => candidate.User.KeycloakSubject)
            .Select(candidate => new StudioMembershipSnapshot(
                candidate.StudioId,
                candidate.User.KeycloakSubject,
                candidate.User.DisplayName,
                candidate.User.Email,
                candidate.Role,
                candidate.CreatedAtUtc,
                candidate.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new StudioMembershipListResult(StudioMembershipListStatus.Success, memberships);
    }

    public async Task<StudioMembershipMutationResult> UpsertMembershipAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        UpsertStudioMembershipCommand command,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var studio = await dbContext.Studios
            .SingleOrDefaultAsync(candidate => candidate.Id == studioId, cancellationToken);

        if (studio is null)
        {
            return new StudioMembershipMutationResult(StudioMembershipMutationStatus.NotFound);
        }

        var actorRole = await GetActorStudioRoleAsync(studioId, actor.Id, cancellationToken);
        if (!CanManageMemberships(actorRole))
        {
            return new StudioMembershipMutationResult(StudioMembershipMutationStatus.Forbidden);
        }

        var targetUser = await dbContext.Users
            .SingleOrDefaultAsync(candidate => candidate.KeycloakSubject == command.MemberKeycloakSubject, cancellationToken);

        if (targetUser is null)
        {
            return new StudioMembershipMutationResult(StudioMembershipMutationStatus.TargetUserNotFound);
        }

        var now = DateTime.UtcNow;
        var membership = await dbContext.StudioMemberships
            .SingleOrDefaultAsync(
                candidate => candidate.StudioId == studioId && candidate.UserId == targetUser.Id,
                cancellationToken);

        if (membership is null)
        {
            membership = new StudioMembership
            {
                StudioId = studioId,
                UserId = targetUser.Id,
                Role = command.Role,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.StudioMemberships.Add(membership);
        }
        else
        {
            if (string.Equals(membership.Role, StudioRoles.Owner, StringComparison.Ordinal) &&
                !string.Equals(command.Role, StudioRoles.Owner, StringComparison.Ordinal) &&
                await IsLastOwnerAsync(studioId, targetUser.Id, cancellationToken))
            {
                return new StudioMembershipMutationResult(StudioMembershipMutationStatus.Conflict);
            }

            membership.Role = command.Role;
            membership.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new StudioMembershipMutationResult(
            StudioMembershipMutationStatus.Success,
            new StudioMembershipSnapshot(
                studioId,
                targetUser.KeycloakSubject,
                targetUser.DisplayName,
                targetUser.Email,
                membership.Role,
                membership.CreatedAtUtc,
                membership.UpdatedAtUtc));
    }

    public async Task<StudioMembershipDeleteStatus> DeleteMembershipAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        string memberKeycloakSubject,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var studioExists = await dbContext.Studios
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Id == studioId, cancellationToken);

        if (!studioExists)
        {
            return StudioMembershipDeleteStatus.NotFound;
        }

        var actorRole = await GetActorStudioRoleAsync(studioId, actor.Id, cancellationToken);
        if (!CanManageMemberships(actorRole))
        {
            return StudioMembershipDeleteStatus.Forbidden;
        }

        var membership = await dbContext.StudioMemberships
            .Include(candidate => candidate.User)
            .SingleOrDefaultAsync(
                candidate => candidate.StudioId == studioId &&
                             candidate.User.KeycloakSubject == memberKeycloakSubject,
                cancellationToken);

        if (membership is null)
        {
            return StudioMembershipDeleteStatus.NotFound;
        }

        if (string.Equals(membership.Role, StudioRoles.Owner, StringComparison.Ordinal) &&
            await IsLastOwnerAsync(studioId, membership.UserId, cancellationToken))
        {
            return StudioMembershipDeleteStatus.Conflict;
        }

        dbContext.StudioMemberships.Remove(membership);
        await dbContext.SaveChangesAsync(cancellationToken);
        return StudioMembershipDeleteStatus.Success;
    }

    private async Task<AppUser> EnsureActorAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        await identityPersistenceService.EnsureCurrentUserProjectionAsync(claims, cancellationToken);
        var subject = GetRequiredSubject(claims);

        return await dbContext.Users.SingleAsync(candidate => candidate.KeycloakSubject == subject, cancellationToken);
    }

    private async Task<string?> GetActorStudioRoleAsync(Guid studioId, Guid actorUserId, CancellationToken cancellationToken) =>
        await dbContext.StudioMemberships
            .Where(candidate => candidate.StudioId == studioId && candidate.UserId == actorUserId)
            .Select(candidate => candidate.Role)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<ManagedStudioAccess> LoadManagedStudioAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        CancellationToken cancellationToken)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var studio = await dbContext.Studios
            .Include(candidate => candidate.Links)
            .SingleOrDefaultAsync(candidate => candidate.Id == studioId, cancellationToken);

        if (studio is null)
        {
            return new ManagedStudioAccess(StudioAccessStatus.NotFound);
        }

        var actorRole = await GetActorStudioRoleAsync(studioId, actor.Id, cancellationToken);
        if (!CanManageStudio(actorRole))
        {
            return new ManagedStudioAccess(StudioAccessStatus.Forbidden);
        }

        return new ManagedStudioAccess(StudioAccessStatus.Success, studio);
    }

    private async Task<bool> IsLastOwnerAsync(Guid studioId, Guid userId, CancellationToken cancellationToken)
    {
        var ownerCount = await dbContext.StudioMemberships
            .Where(candidate => candidate.StudioId == studioId && candidate.Role == StudioRoles.Owner)
            .CountAsync(cancellationToken);

        var targetIsOwner = await dbContext.StudioMemberships
            .AnyAsync(
                candidate => candidate.StudioId == studioId &&
                             candidate.UserId == userId &&
                             candidate.Role == StudioRoles.Owner,
                cancellationToken);

        return targetIsOwner && ownerCount <= 1;
    }

    private static bool CanManageStudio(string? role) =>
        string.Equals(role, StudioRoles.Owner, StringComparison.Ordinal) ||
        string.Equals(role, StudioRoles.Admin, StringComparison.Ordinal);

    private static bool CanManageMemberships(string? role) => CanManageStudio(role);

    private static StudioSnapshot MapStudio(Studio studio) =>
        new(
            studio.Id,
            studio.Slug,
            studio.DisplayName,
            studio.Description,
            studio.LogoUrl,
            studio.BannerUrl,
            studio.Links
                .OrderBy(candidate => candidate.Label)
                .ThenBy(candidate => candidate.Url)
                .Select(MapStudioLink)
                .ToArray(),
            studio.CreatedAtUtc,
            studio.UpdatedAtUtc);

    private static StudioSummarySnapshot MapStudioSummary(Studio studio) =>
        new(
            studio.Id,
            studio.Slug,
            studio.DisplayName,
            studio.Description,
            studio.LogoUrl,
            studio.BannerUrl,
            studio.Links
                .OrderBy(candidate => candidate.Label)
                .ThenBy(candidate => candidate.Url)
                .Select(MapStudioLink)
                .ToList());

    private static StudioLinkSnapshot MapStudioLink(StudioLink link) =>
        new(
            link.Id,
            link.Label,
            link.Url,
            link.CreatedAtUtc,
            link.UpdatedAtUtc);

    private static string GetRequiredSubject(IEnumerable<Claim> claims)
    {
        var subject = ClaimValueResolver.GetSubject(claims);

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new InvalidOperationException("Authenticated user is missing the required subject claim.");
        }

        return subject;
    }
}

internal static class StudioRoles
{
    public const string Owner = "owner";
    public const string Admin = "admin";
    public const string Editor = "editor";
}

internal static class StudioMediaRoles
{
    public const string Logo = "logo";
    public const string Banner = "banner";
}

internal enum StudioAccessStatus
{
    Success,
    NotFound,
    Forbidden
}

internal sealed record ManagedStudioAccess(StudioAccessStatus Status, Studio? Studio = null);

/// <summary>
/// Command payload for creating a studio.
/// </summary>
/// <param name="Slug">Studio route key.</param>
/// <param name="DisplayName">Public studio name.</param>
/// <param name="Description">Optional public description.</param>
/// <param name="LogoUrl">Optional public logo URL.</param>
/// <param name="BannerUrl">Optional public banner URL.</param>
internal sealed record CreateStudioCommand(string Slug, string DisplayName, string? Description, string? LogoUrl, string? BannerUrl);

/// <summary>
/// Command payload for updating a studio.
/// </summary>
/// <param name="Slug">Studio route key.</param>
/// <param name="DisplayName">Public studio name.</param>
/// <param name="Description">Optional public description.</param>
/// <param name="LogoUrl">Optional public logo URL.</param>
/// <param name="BannerUrl">Optional public banner URL.</param>
internal sealed record UpdateStudioCommand(string Slug, string DisplayName, string? Description, string? LogoUrl, string? BannerUrl);

/// <summary>
/// Command payload for creating or updating a studio link.
/// </summary>
/// <param name="Label">Player-facing label.</param>
/// <param name="Url">Absolute destination URL.</param>
internal sealed record UpsertStudioLinkCommand(string Label, string Url);

/// <summary>
/// Command payload for adding or updating a studio membership.
/// </summary>
/// <param name="MemberKeycloakSubject">Target member Keycloak subject.</param>
/// <param name="Role">Studio-scoped membership role.</param>
internal sealed record UpsertStudioMembershipCommand(string MemberKeycloakSubject, string Role);

/// <summary>
/// Public studio summary projection.
/// </summary>
/// <param name="Id">Studio identifier.</param>
/// <param name="Slug">Studio route key.</param>
/// <param name="DisplayName">Public studio name.</param>
/// <param name="Description">Optional public description.</param>
/// <param name="LogoUrl">Optional public logo URL.</param>
/// <param name="BannerUrl">Optional public banner URL.</param>
/// <param name="Links">Public studio links.</param>
internal sealed record StudioSummarySnapshot(
    Guid Id,
    string Slug,
    string DisplayName,
    string? Description,
    string? LogoUrl,
    string? BannerUrl,
    IReadOnlyList<StudioLinkSnapshot> Links);

/// <summary>
/// Developer-visible managed studio summary projection.
/// </summary>
/// <param name="Id">Studio identifier.</param>
/// <param name="Slug">Studio route key.</param>
/// <param name="DisplayName">Studio display name.</param>
/// <param name="Description">Optional public description.</param>
/// <param name="LogoUrl">Optional public logo URL.</param>
/// <param name="BannerUrl">Optional public banner URL.</param>
/// <param name="Links">Configured public studio links.</param>
/// <param name="Role">Caller membership role within the studio.</param>
internal sealed record DeveloperStudioSummarySnapshot(
    Guid Id,
    string Slug,
    string DisplayName,
    string? Description,
    string? LogoUrl,
    string? BannerUrl,
    IReadOnlyList<StudioLinkSnapshot> Links,
    string Role);

/// <summary>
/// Detailed studio projection.
/// </summary>
/// <param name="Id">Studio identifier.</param>
/// <param name="Slug">Studio route key.</param>
/// <param name="DisplayName">Public studio name.</param>
/// <param name="Description">Optional public description.</param>
/// <param name="LogoUrl">Optional public logo URL.</param>
/// <param name="BannerUrl">Optional public banner URL.</param>
/// <param name="Links">Configured public studio links.</param>
/// <param name="CreatedAtUtc">UTC creation timestamp.</param>
/// <param name="UpdatedAtUtc">UTC update timestamp.</param>
internal sealed record StudioSnapshot(
    Guid Id,
    string Slug,
    string DisplayName,
    string? Description,
    string? LogoUrl,
    string? BannerUrl,
    IReadOnlyList<StudioLinkSnapshot> Links,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>
/// Studio link projection.
/// </summary>
/// <param name="Id">Studio link identifier.</param>
/// <param name="Label">Player-facing label.</param>
/// <param name="Url">Absolute destination URL.</param>
/// <param name="CreatedAtUtc">UTC creation timestamp.</param>
/// <param name="UpdatedAtUtc">UTC update timestamp.</param>
internal sealed record StudioLinkSnapshot(
    Guid Id,
    string Label,
    string Url,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>
/// Studio membership projection.
/// </summary>
/// <param name="StudioId">Studio identifier.</param>
/// <param name="KeycloakSubject">Target member Keycloak subject.</param>
/// <param name="DisplayName">Cached member display name.</param>
/// <param name="Email">Cached member email address.</param>
/// <param name="Role">Studio-scoped membership role.</param>
/// <param name="JoinedAtUtc">UTC timestamp when the member joined.</param>
/// <param name="UpdatedAtUtc">UTC timestamp when the membership last changed.</param>
internal sealed record StudioMembershipSnapshot(
    Guid StudioId,
    string KeycloakSubject,
    string? DisplayName,
    string? Email,
    string Role,
    DateTime JoinedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>
/// Result wrapper for studio mutations.
/// </summary>
/// <param name="Status">Operation status.</param>
/// <param name="Studio">Returned studio snapshot when available.</param>
internal sealed record StudioMutationResult(
    StudioMutationStatus Status,
    StudioSnapshot? Studio = null);

/// <summary>
/// Result wrapper for studio link listings.
/// </summary>
/// <param name="Status">Operation status.</param>
/// <param name="Links">Returned links when available.</param>
internal sealed record StudioLinkListResult(
    StudioLinkListStatus Status,
    IReadOnlyList<StudioLinkSnapshot>? Links = null);

/// <summary>
/// Result wrapper for studio link mutations.
/// </summary>
/// <param name="Status">Operation status.</param>
/// <param name="Link">Returned link when available.</param>
internal sealed record StudioLinkMutationResult(
    StudioLinkMutationStatus Status,
    StudioLinkSnapshot? Link = null);

/// <summary>
/// Result wrapper for membership listings.
/// </summary>
/// <param name="Status">Operation status.</param>
/// <param name="Memberships">Returned memberships when available.</param>
internal sealed record StudioMembershipListResult(
    StudioMembershipListStatus Status,
    IReadOnlyList<StudioMembershipSnapshot>? Memberships = null);

/// <summary>
/// Result wrapper for membership mutations.
/// </summary>
/// <param name="Status">Operation status.</param>
/// <param name="Membership">Returned membership snapshot when available.</param>
internal sealed record StudioMembershipMutationResult(
    StudioMembershipMutationStatus Status,
    StudioMembershipSnapshot? Membership = null);

/// <summary>
/// Outcome codes for studio create/update/get operations.
/// </summary>
internal enum StudioMutationStatus
{
    Success,
    NotFound,
    Forbidden,
    Conflict
}

/// <summary>
/// Outcome codes for studio deletion.
/// </summary>
internal enum StudioDeleteStatus
{
    Success,
    NotFound,
    Forbidden
}

/// <summary>
/// Outcome codes for studio link listings.
/// </summary>
internal enum StudioLinkListStatus
{
    Success,
    NotFound,
    Forbidden
}

/// <summary>
/// Outcome codes for studio link mutations.
/// </summary>
internal enum StudioLinkMutationStatus
{
    Success,
    NotFound,
    Forbidden
}

/// <summary>
/// Outcome codes for studio link deletion.
/// </summary>
internal enum StudioLinkDeleteStatus
{
    Success,
    NotFound,
    Forbidden
}

/// <summary>
/// Outcome codes for membership listings.
/// </summary>
internal enum StudioMembershipListStatus
{
    Success,
    NotFound,
    Forbidden
}

/// <summary>
/// Outcome codes for membership mutations.
/// </summary>
internal enum StudioMembershipMutationStatus
{
    Success,
    NotFound,
    Forbidden,
    Conflict,
    TargetUserNotFound
}

/// <summary>
/// Outcome codes for membership deletion.
/// </summary>
internal enum StudioMembershipDeleteStatus
{
    Success,
    NotFound,
    Forbidden,
    Conflict
}
