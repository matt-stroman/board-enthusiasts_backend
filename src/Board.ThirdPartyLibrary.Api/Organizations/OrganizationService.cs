using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Auth;
using Board.ThirdPartyLibrary.Api.Identity;
using Board.ThirdPartyLibrary.Api.Persistence;
using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Board.ThirdPartyLibrary.Api.Organizations;

internal interface IOrganizationService
{
    Task<IReadOnlyList<OrganizationSummarySnapshot>> ListOrganizationsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeveloperOrganizationSummarySnapshot>> ListManagedOrganizationsAsync(
        IEnumerable<Claim> claims,
        CancellationToken cancellationToken = default);

    Task<OrganizationSnapshot?> GetOrganizationBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<OrganizationMutationResult> CreateOrganizationAsync(
        IEnumerable<Claim> claims,
        CreateOrganizationCommand command,
        CancellationToken cancellationToken = default);

    Task<OrganizationMutationResult> UpdateOrganizationAsync(
        IEnumerable<Claim> claims,
        Guid organizationId,
        UpdateOrganizationCommand command,
        CancellationToken cancellationToken = default);

    Task<OrganizationDeleteStatus> DeleteOrganizationAsync(
        IEnumerable<Claim> claims,
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<OrganizationMembershipListResult> ListMembershipsAsync(
        IEnumerable<Claim> claims,
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<OrganizationMembershipMutationResult> UpsertMembershipAsync(
        IEnumerable<Claim> claims,
        Guid organizationId,
        UpsertOrganizationMembershipCommand command,
        CancellationToken cancellationToken = default);

    Task<OrganizationMembershipDeleteStatus> DeleteMembershipAsync(
        IEnumerable<Claim> claims,
        Guid organizationId,
        string memberKeycloakSubject,
        CancellationToken cancellationToken = default);
}

internal sealed class OrganizationService(
    BoardLibraryDbContext dbContext,
    IIdentityPersistenceService identityPersistenceService) : IOrganizationService
{
    public async Task<IReadOnlyList<OrganizationSummarySnapshot>> ListOrganizationsAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Organizations
            .AsNoTracking()
            .OrderBy(candidate => candidate.DisplayName)
            .Select(candidate => new OrganizationSummarySnapshot(
                candidate.Id,
                candidate.Slug,
                candidate.DisplayName,
                candidate.Description,
                candidate.LogoUrl))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DeveloperOrganizationSummarySnapshot>> ListManagedOrganizationsAsync(
        IEnumerable<Claim> claims,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);

        return await dbContext.OrganizationMemberships
            .AsNoTracking()
            .Where(candidate =>
                candidate.UserId == actor.Id &&
                (candidate.Role == OrganizationRoles.Owner ||
                 candidate.Role == OrganizationRoles.Admin ||
                 candidate.Role == OrganizationRoles.Editor))
            .Include(candidate => candidate.Organization)
            .OrderBy(candidate => candidate.Organization.DisplayName)
            .Select(candidate => new DeveloperOrganizationSummarySnapshot(
                candidate.OrganizationId,
                candidate.Organization.Slug,
                candidate.Organization.DisplayName,
                candidate.Organization.Description,
                candidate.Organization.LogoUrl,
                candidate.Role))
            .ToListAsync(cancellationToken);
    }

    public async Task<OrganizationSnapshot?> GetOrganizationBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Slug == slug, cancellationToken);

        return organization is null ? null : MapOrganization(organization);
    }

    public async Task<OrganizationMutationResult> CreateOrganizationAsync(
        IEnumerable<Claim> claims,
        CreateOrganizationCommand command,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var now = DateTime.UtcNow;
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Slug = command.Slug,
            DisplayName = command.DisplayName,
            Description = command.Description,
            LogoUrl = command.LogoUrl,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var membership = new OrganizationMembership
        {
            OrganizationId = organization.Id,
            UserId = actor.Id,
            Role = OrganizationRoles.Owner,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Organizations.Add(organization);
        dbContext.OrganizationMemberships.Add(membership);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new OrganizationMutationResult(OrganizationMutationStatus.Success, MapOrganization(organization));
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(organization).State = EntityState.Detached;
            dbContext.Entry(membership).State = EntityState.Detached;
            return new OrganizationMutationResult(OrganizationMutationStatus.Conflict);
        }
    }

    public async Task<OrganizationMutationResult> UpdateOrganizationAsync(
        IEnumerable<Claim> claims,
        Guid organizationId,
        UpdateOrganizationCommand command,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(candidate => candidate.Id == organizationId, cancellationToken);

        if (organization is null)
        {
            return new OrganizationMutationResult(OrganizationMutationStatus.NotFound);
        }

        var actorRole = await GetActorOrganizationRoleAsync(organizationId, actor.Id, cancellationToken);
        if (!CanManageOrganization(actorRole))
        {
            return new OrganizationMutationResult(OrganizationMutationStatus.Forbidden);
        }

        organization.Slug = command.Slug;
        organization.DisplayName = command.DisplayName;
        organization.Description = command.Description;
        organization.LogoUrl = command.LogoUrl;
        organization.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new OrganizationMutationResult(OrganizationMutationStatus.Success, MapOrganization(organization));
        }
        catch (DbUpdateException)
        {
            return new OrganizationMutationResult(OrganizationMutationStatus.Conflict);
        }
    }

    public async Task<OrganizationDeleteStatus> DeleteOrganizationAsync(
        IEnumerable<Claim> claims,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(candidate => candidate.Id == organizationId, cancellationToken);

        if (organization is null)
        {
            return OrganizationDeleteStatus.NotFound;
        }

        var actorRole = await GetActorOrganizationRoleAsync(organizationId, actor.Id, cancellationToken);
        if (!string.Equals(actorRole, OrganizationRoles.Owner, StringComparison.Ordinal))
        {
            return OrganizationDeleteStatus.Forbidden;
        }

        var memberships = await dbContext.OrganizationMemberships
            .Where(candidate => candidate.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

        dbContext.OrganizationMemberships.RemoveRange(memberships);
        dbContext.Organizations.Remove(organization);
        await dbContext.SaveChangesAsync(cancellationToken);
        return OrganizationDeleteStatus.Success;
    }

    public async Task<OrganizationMembershipListResult> ListMembershipsAsync(
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
            return new OrganizationMembershipListResult(OrganizationMembershipListStatus.NotFound);
        }

        var actorRole = await GetActorOrganizationRoleAsync(organizationId, actor.Id, cancellationToken);
        if (!CanManageMemberships(actorRole))
        {
            return new OrganizationMembershipListResult(OrganizationMembershipListStatus.Forbidden);
        }

        var memberships = await dbContext.OrganizationMemberships
            .AsNoTracking()
            .Where(candidate => candidate.OrganizationId == organizationId)
            .Include(candidate => candidate.User)
            .OrderBy(candidate => candidate.Role)
            .ThenBy(candidate => candidate.User.DisplayName)
            .ThenBy(candidate => candidate.User.KeycloakSubject)
            .Select(candidate => new OrganizationMembershipSnapshot(
                candidate.OrganizationId,
                candidate.User.KeycloakSubject,
                candidate.User.DisplayName,
                candidate.User.Email,
                candidate.Role,
                candidate.CreatedAtUtc,
                candidate.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new OrganizationMembershipListResult(OrganizationMembershipListStatus.Success, memberships);
    }

    public async Task<OrganizationMembershipMutationResult> UpsertMembershipAsync(
        IEnumerable<Claim> claims,
        Guid organizationId,
        UpsertOrganizationMembershipCommand command,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(candidate => candidate.Id == organizationId, cancellationToken);

        if (organization is null)
        {
            return new OrganizationMembershipMutationResult(OrganizationMembershipMutationStatus.NotFound);
        }

        var actorRole = await GetActorOrganizationRoleAsync(organizationId, actor.Id, cancellationToken);
        if (!CanManageMemberships(actorRole))
        {
            return new OrganizationMembershipMutationResult(OrganizationMembershipMutationStatus.Forbidden);
        }

        var targetUser = await dbContext.Users
            .SingleOrDefaultAsync(candidate => candidate.KeycloakSubject == command.MemberKeycloakSubject, cancellationToken);

        if (targetUser is null)
        {
            return new OrganizationMembershipMutationResult(OrganizationMembershipMutationStatus.TargetUserNotFound);
        }

        var now = DateTime.UtcNow;
        var membership = await dbContext.OrganizationMemberships
            .SingleOrDefaultAsync(
                candidate => candidate.OrganizationId == organizationId && candidate.UserId == targetUser.Id,
                cancellationToken);

        if (membership is null)
        {
            membership = new OrganizationMembership
            {
                OrganizationId = organizationId,
                UserId = targetUser.Id,
                Role = command.Role,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.OrganizationMemberships.Add(membership);
        }
        else
        {
            if (string.Equals(membership.Role, OrganizationRoles.Owner, StringComparison.Ordinal) &&
                !string.Equals(command.Role, OrganizationRoles.Owner, StringComparison.Ordinal) &&
                await IsLastOwnerAsync(organizationId, targetUser.Id, cancellationToken))
            {
                return new OrganizationMembershipMutationResult(OrganizationMembershipMutationStatus.Conflict);
            }

            membership.Role = command.Role;
            membership.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new OrganizationMembershipMutationResult(
            OrganizationMembershipMutationStatus.Success,
            new OrganizationMembershipSnapshot(
                organizationId,
                targetUser.KeycloakSubject,
                targetUser.DisplayName,
                targetUser.Email,
                membership.Role,
                membership.CreatedAtUtc,
                membership.UpdatedAtUtc));
    }

    public async Task<OrganizationMembershipDeleteStatus> DeleteMembershipAsync(
        IEnumerable<Claim> claims,
        Guid organizationId,
        string memberKeycloakSubject,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var organizationExists = await dbContext.Organizations
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Id == organizationId, cancellationToken);

        if (!organizationExists)
        {
            return OrganizationMembershipDeleteStatus.NotFound;
        }

        var actorRole = await GetActorOrganizationRoleAsync(organizationId, actor.Id, cancellationToken);
        if (!CanManageMemberships(actorRole))
        {
            return OrganizationMembershipDeleteStatus.Forbidden;
        }

        var membership = await dbContext.OrganizationMemberships
            .Include(candidate => candidate.User)
            .SingleOrDefaultAsync(
                candidate => candidate.OrganizationId == organizationId &&
                             candidate.User.KeycloakSubject == memberKeycloakSubject,
                cancellationToken);

        if (membership is null)
        {
            return OrganizationMembershipDeleteStatus.NotFound;
        }

        if (string.Equals(membership.Role, OrganizationRoles.Owner, StringComparison.Ordinal) &&
            await IsLastOwnerAsync(organizationId, membership.UserId, cancellationToken))
        {
            return OrganizationMembershipDeleteStatus.Conflict;
        }

        dbContext.OrganizationMemberships.Remove(membership);
        await dbContext.SaveChangesAsync(cancellationToken);
        return OrganizationMembershipDeleteStatus.Success;
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

    private async Task<bool> IsLastOwnerAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var ownerCount = await dbContext.OrganizationMemberships
            .Where(candidate => candidate.OrganizationId == organizationId && candidate.Role == OrganizationRoles.Owner)
            .CountAsync(cancellationToken);

        var targetIsOwner = await dbContext.OrganizationMemberships
            .AnyAsync(
                candidate => candidate.OrganizationId == organizationId &&
                             candidate.UserId == userId &&
                             candidate.Role == OrganizationRoles.Owner,
                cancellationToken);

        return targetIsOwner && ownerCount <= 1;
    }

    private static bool CanManageOrganization(string? role) =>
        string.Equals(role, OrganizationRoles.Owner, StringComparison.Ordinal) ||
        string.Equals(role, OrganizationRoles.Admin, StringComparison.Ordinal);

    private static bool CanManageMemberships(string? role) => CanManageOrganization(role);

    private static OrganizationSnapshot MapOrganization(Organization organization) =>
        new(
            organization.Id,
            organization.Slug,
            organization.DisplayName,
            organization.Description,
            organization.LogoUrl,
            organization.CreatedAtUtc,
            organization.UpdatedAtUtc);

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

internal static class OrganizationRoles
{
    public const string Owner = "owner";
    public const string Admin = "admin";
    public const string Editor = "editor";
}

/// <summary>
/// Command payload for creating an organization.
/// </summary>
/// <param name="Slug">Organization route key.</param>
/// <param name="DisplayName">Public organization name.</param>
/// <param name="Description">Optional public description.</param>
/// <param name="LogoUrl">Optional public logo URL.</param>
internal sealed record CreateOrganizationCommand(string Slug, string DisplayName, string? Description, string? LogoUrl);

/// <summary>
/// Command payload for updating an organization.
/// </summary>
/// <param name="Slug">Organization route key.</param>
/// <param name="DisplayName">Public organization name.</param>
/// <param name="Description">Optional public description.</param>
/// <param name="LogoUrl">Optional public logo URL.</param>
internal sealed record UpdateOrganizationCommand(string Slug, string DisplayName, string? Description, string? LogoUrl);

/// <summary>
/// Command payload for adding or updating an organization membership.
/// </summary>
/// <param name="MemberKeycloakSubject">Target member Keycloak subject.</param>
/// <param name="Role">Organization-scoped membership role.</param>
internal sealed record UpsertOrganizationMembershipCommand(string MemberKeycloakSubject, string Role);

/// <summary>
/// Public organization summary projection.
/// </summary>
/// <param name="Id">Organization identifier.</param>
/// <param name="Slug">Organization route key.</param>
/// <param name="DisplayName">Public organization name.</param>
/// <param name="Description">Optional public description.</param>
/// <param name="LogoUrl">Optional public logo URL.</param>
internal sealed record OrganizationSummarySnapshot(
    Guid Id,
    string Slug,
    string DisplayName,
    string? Description,
    string? LogoUrl);

/// <summary>
/// Developer-visible managed organization summary projection.
/// </summary>
/// <param name="Id">Organization identifier.</param>
/// <param name="Slug">Organization route key.</param>
/// <param name="DisplayName">Organization display name.</param>
/// <param name="Description">Optional public description.</param>
/// <param name="LogoUrl">Optional public logo URL.</param>
/// <param name="Role">Caller membership role within the organization.</param>
internal sealed record DeveloperOrganizationSummarySnapshot(
    Guid Id,
    string Slug,
    string DisplayName,
    string? Description,
    string? LogoUrl,
    string Role);

/// <summary>
/// Detailed organization projection.
/// </summary>
/// <param name="Id">Organization identifier.</param>
/// <param name="Slug">Organization route key.</param>
/// <param name="DisplayName">Public organization name.</param>
/// <param name="Description">Optional public description.</param>
/// <param name="LogoUrl">Optional public logo URL.</param>
/// <param name="CreatedAtUtc">UTC creation timestamp.</param>
/// <param name="UpdatedAtUtc">UTC update timestamp.</param>
internal sealed record OrganizationSnapshot(
    Guid Id,
    string Slug,
    string DisplayName,
    string? Description,
    string? LogoUrl,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>
/// Organization membership projection.
/// </summary>
/// <param name="OrganizationId">Organization identifier.</param>
/// <param name="KeycloakSubject">Target member Keycloak subject.</param>
/// <param name="DisplayName">Cached member display name.</param>
/// <param name="Email">Cached member email address.</param>
/// <param name="Role">Organization-scoped membership role.</param>
/// <param name="JoinedAtUtc">UTC timestamp when the member joined.</param>
/// <param name="UpdatedAtUtc">UTC timestamp when the membership last changed.</param>
internal sealed record OrganizationMembershipSnapshot(
    Guid OrganizationId,
    string KeycloakSubject,
    string? DisplayName,
    string? Email,
    string Role,
    DateTime JoinedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>
/// Result wrapper for organization mutations.
/// </summary>
/// <param name="Status">Operation status.</param>
/// <param name="Organization">Returned organization snapshot when available.</param>
internal sealed record OrganizationMutationResult(
    OrganizationMutationStatus Status,
    OrganizationSnapshot? Organization = null);

/// <summary>
/// Result wrapper for membership listings.
/// </summary>
/// <param name="Status">Operation status.</param>
/// <param name="Memberships">Returned memberships when available.</param>
internal sealed record OrganizationMembershipListResult(
    OrganizationMembershipListStatus Status,
    IReadOnlyList<OrganizationMembershipSnapshot>? Memberships = null);

/// <summary>
/// Result wrapper for membership mutations.
/// </summary>
/// <param name="Status">Operation status.</param>
/// <param name="Membership">Returned membership snapshot when available.</param>
internal sealed record OrganizationMembershipMutationResult(
    OrganizationMembershipMutationStatus Status,
    OrganizationMembershipSnapshot? Membership = null);

/// <summary>
/// Outcome codes for organization create/update/get operations.
/// </summary>
internal enum OrganizationMutationStatus
{
    Success,
    NotFound,
    Forbidden,
    Conflict
}

/// <summary>
/// Outcome codes for organization deletion.
/// </summary>
internal enum OrganizationDeleteStatus
{
    Success,
    NotFound,
    Forbidden
}

/// <summary>
/// Outcome codes for membership listings.
/// </summary>
internal enum OrganizationMembershipListStatus
{
    Success,
    NotFound,
    Forbidden
}

/// <summary>
/// Outcome codes for membership mutations.
/// </summary>
internal enum OrganizationMembershipMutationStatus
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
internal enum OrganizationMembershipDeleteStatus
{
    Success,
    NotFound,
    Forbidden,
    Conflict
}
