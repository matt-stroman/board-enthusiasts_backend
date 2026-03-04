using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Auth;
using Board.ThirdPartyLibrary.Api.Persistence;
using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Board.ThirdPartyLibrary.Api.Identity;

/// <summary>
/// Developer enrollment workflow contract.
/// </summary>
internal interface IDeveloperEnrollmentService
{
    /// <summary>
    /// Gets the current caller's developer enrollment state.
    /// </summary>
    Task<DeveloperEnrollmentStateSnapshot> GetCurrentEnrollmentAsync(
        IEnumerable<Claim> claims,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits or returns the current caller's developer enrollment request.
    /// </summary>
    Task<DeveloperEnrollmentStateSnapshot> SubmitEnrollmentAsync(
        IEnumerable<Claim> claims,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists developer enrollment requests for moderators.
    /// </summary>
    Task<DeveloperEnrollmentRequestListResult> ListRequestsAsync(
        IEnumerable<Claim> claims,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reviews a developer enrollment request as a moderator.
    /// </summary>
    Task<DeveloperEnrollmentReviewResult> ReviewRequestAsync(
        IEnumerable<Claim> claims,
        Guid requestId,
        DeveloperEnrollmentReviewDecision decision,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the caller currently has developer access.
    /// </summary>
    Task<bool> HasDeveloperAccessAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);
}

/// <summary>
/// Entity Framework-backed developer enrollment workflow service.
/// </summary>
internal sealed class DeveloperEnrollmentService(
    BoardLibraryDbContext dbContext,
    IIdentityPersistenceService identityPersistenceService,
    IKeycloakUserRoleClient keycloakUserRoleClient) : IDeveloperEnrollmentService
{
    /// <inheritdoc />
    public async Task<DeveloperEnrollmentStateSnapshot> GetCurrentEnrollmentAsync(
        IEnumerable<Claim> claims,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var request = await dbContext.DeveloperEnrollmentRequests
            .AsNoTracking()
            .Include(candidate => candidate.ReviewedByUser)
            .SingleOrDefaultAsync(candidate => candidate.UserId == actor.Id, cancellationToken);

        return MapCurrentEnrollment(request, HasImmediateDeveloperAccess(claims));
    }

    /// <inheritdoc />
    public async Task<DeveloperEnrollmentStateSnapshot> SubmitEnrollmentAsync(
        IEnumerable<Claim> claims,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var request = await dbContext.DeveloperEnrollmentRequests
            .Include(candidate => candidate.ReviewedByUser)
            .SingleOrDefaultAsync(candidate => candidate.UserId == actor.Id, cancellationToken);

        if (HasImmediateDeveloperAccess(claims))
        {
            return MapCurrentEnrollment(request, developerAccessEnabledOverride: true);
        }

        if (request is not null)
        {
            return MapCurrentEnrollment(request, developerAccessEnabledOverride: false);
        }

        var now = DateTime.UtcNow;
        request = new DeveloperEnrollmentRequest
        {
            Id = Guid.NewGuid(),
            UserId = actor.Id,
            Status = DeveloperEnrollmentStatuses.Pending,
            RequestedAtUtc = now,
            ReviewedAtUtc = null,
            ReviewedByUserId = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.DeveloperEnrollmentRequests.Add(request);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(request).State = EntityState.Detached;

            request = await dbContext.DeveloperEnrollmentRequests
                .AsNoTracking()
                .Include(candidate => candidate.ReviewedByUser)
                .SingleAsync(candidate => candidate.UserId == actor.Id, cancellationToken);
        }

        return MapCurrentEnrollment(request, developerAccessEnabledOverride: false);
    }

    /// <inheritdoc />
    public async Task<DeveloperEnrollmentRequestListResult> ListRequestsAsync(
        IEnumerable<Claim> claims,
        CancellationToken cancellationToken = default)
    {
        if (!HasModeratorAccess(claims))
        {
            return new DeveloperEnrollmentRequestListResult(DeveloperEnrollmentRequestListStatus.Forbidden);
        }

        await EnsureActorAsync(claims, cancellationToken);

        var requests = await dbContext.DeveloperEnrollmentRequests
            .AsNoTracking()
            .Include(candidate => candidate.User)
            .Include(candidate => candidate.ReviewedByUser)
            .OrderBy(candidate => candidate.Status == DeveloperEnrollmentStatuses.Pending ? 0 : 1)
            .ThenByDescending(candidate => candidate.RequestedAtUtc)
            .Select(candidate => new DeveloperEnrollmentRequestSnapshot(
                candidate.Id,
                candidate.User.KeycloakSubject,
                candidate.User.DisplayName,
                candidate.User.Email,
                candidate.Status,
                candidate.Status == DeveloperEnrollmentStatuses.Approved,
                candidate.RequestedAtUtc,
                candidate.ReviewedAtUtc,
                candidate.ReviewedByUser == null ? null : candidate.ReviewedByUser.KeycloakSubject))
            .ToListAsync(cancellationToken);

        return new DeveloperEnrollmentRequestListResult(DeveloperEnrollmentRequestListStatus.Success, requests);
    }

    /// <inheritdoc />
    public async Task<DeveloperEnrollmentReviewResult> ReviewRequestAsync(
        IEnumerable<Claim> claims,
        Guid requestId,
        DeveloperEnrollmentReviewDecision decision,
        CancellationToken cancellationToken = default)
    {
        if (!HasModeratorAccess(claims))
        {
            return new DeveloperEnrollmentReviewResult(DeveloperEnrollmentReviewStatus.Forbidden);
        }

        var reviewer = await EnsureActorAsync(claims, cancellationToken);
        var request = await dbContext.DeveloperEnrollmentRequests
            .Include(candidate => candidate.User)
            .Include(candidate => candidate.ReviewedByUser)
            .SingleOrDefaultAsync(candidate => candidate.Id == requestId, cancellationToken);

        if (request is null)
        {
            return new DeveloperEnrollmentReviewResult(DeveloperEnrollmentReviewStatus.NotFound);
        }

        if (!string.Equals(request.Status, DeveloperEnrollmentStatuses.Pending, StringComparison.Ordinal))
        {
            return new DeveloperEnrollmentReviewResult(DeveloperEnrollmentReviewStatus.Conflict);
        }

        if (decision == DeveloperEnrollmentReviewDecision.Approve)
        {
            var roleAssignment = await keycloakUserRoleClient.EnsureRealmRoleAssignedAsync(
                request.User.KeycloakSubject,
                "developer",
                cancellationToken);

            if (!roleAssignment.Succeeded)
            {
                return new DeveloperEnrollmentReviewResult(
                    DeveloperEnrollmentReviewStatus.UpstreamFailure,
                    ErrorDetail: roleAssignment.ErrorDetail ?? "Keycloak role assignment failed for the authenticated user.");
            }

            request.Status = DeveloperEnrollmentStatuses.Approved;
        }
        else
        {
            request.Status = DeveloperEnrollmentStatuses.Rejected;
        }

        var now = DateTime.UtcNow;
        request.ReviewedAtUtc = now;
        request.ReviewedByUserId = reviewer.Id;
        request.ReviewedByUser = reviewer;
        request.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new DeveloperEnrollmentReviewResult(
            DeveloperEnrollmentReviewStatus.Success,
            MapRequest(request));
    }

    /// <inheritdoc />
    public async Task<bool> HasDeveloperAccessAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        if (HasImmediateDeveloperAccess(claims))
        {
            return true;
        }

        var actor = await EnsureActorAsync(claims, cancellationToken);
        var status = await dbContext.DeveloperEnrollmentRequests
            .AsNoTracking()
            .Where(candidate => candidate.UserId == actor.Id)
            .Select(candidate => candidate.Status)
            .SingleOrDefaultAsync(cancellationToken);

        return string.Equals(status, DeveloperEnrollmentStatuses.Approved, StringComparison.Ordinal);
    }

    private async Task<AppUser> EnsureActorAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        await identityPersistenceService.EnsureCurrentUserProjectionAsync(claims, cancellationToken);
        var subject = GetRequiredSubject(claims);

        return await dbContext.Users
            .SingleAsync(candidate => candidate.KeycloakSubject == subject, cancellationToken);
    }

    private static DeveloperEnrollmentStateSnapshot MapCurrentEnrollment(
        DeveloperEnrollmentRequest? request,
        bool developerAccessEnabledOverride)
    {
        if (developerAccessEnabledOverride)
        {
            return new DeveloperEnrollmentStateSnapshot(
                request?.Id,
                DeveloperEnrollmentStatuses.Approved,
                true,
                false,
                request?.RequestedAtUtc,
                request?.ReviewedAtUtc,
                request?.ReviewedByUser?.KeycloakSubject);
        }

        if (request is null)
        {
            return new DeveloperEnrollmentStateSnapshot(
                null,
                DeveloperEnrollmentStatuses.NotRequested,
                false,
                true,
                null,
                null,
                null);
        }

        return new DeveloperEnrollmentStateSnapshot(
            request.Id,
            request.Status,
            string.Equals(request.Status, DeveloperEnrollmentStatuses.Approved, StringComparison.Ordinal),
            false,
            request.RequestedAtUtc,
            request.ReviewedAtUtc,
            request.ReviewedByUser?.KeycloakSubject);
    }

    private static DeveloperEnrollmentRequestSnapshot MapRequest(DeveloperEnrollmentRequest request) =>
        new(
            request.Id,
            request.User.KeycloakSubject,
            request.User.DisplayName,
            request.User.Email,
            request.Status,
            string.Equals(request.Status, DeveloperEnrollmentStatuses.Approved, StringComparison.Ordinal),
            request.RequestedAtUtc,
            request.ReviewedAtUtc,
            request.ReviewedByUser?.KeycloakSubject);

    private static bool HasImmediateDeveloperAccess(IEnumerable<Claim> claims) =>
        HasRole(claims, "developer") || HasRole(claims, "admin");

    private static bool HasModeratorAccess(IEnumerable<Claim> claims) =>
        HasRole(claims, "moderator") || HasRole(claims, "admin");

    private static bool HasRole(IEnumerable<Claim> claims, string role) =>
        claims.Any(claim =>
            claim.Type == ClaimTypes.Role &&
            string.Equals(claim.Value, role, StringComparison.OrdinalIgnoreCase));

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

/// <summary>
/// Stable developer enrollment status values.
/// </summary>
internal static class DeveloperEnrollmentStatuses
{
    public const string NotRequested = "not_requested";
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

/// <summary>
/// Current developer enrollment state for the authenticated caller.
/// </summary>
internal sealed record DeveloperEnrollmentStateSnapshot(
    Guid? RequestId,
    string Status,
    bool DeveloperAccessEnabled,
    bool CanSubmitRequest,
    DateTime? RequestedAtUtc,
    DateTime? ReviewedAtUtc,
    string? ReviewerSubject);

/// <summary>
/// Moderator-visible developer enrollment request.
/// </summary>
internal sealed record DeveloperEnrollmentRequestSnapshot(
    Guid RequestId,
    string ApplicantSubject,
    string? ApplicantDisplayName,
    string? ApplicantEmail,
    string Status,
    bool DeveloperAccessEnabled,
    DateTime RequestedAtUtc,
    DateTime? ReviewedAtUtc,
    string? ReviewerSubject);

/// <summary>
/// Review outcome for developer enrollment requests.
/// </summary>
internal sealed record DeveloperEnrollmentReviewResult(
    DeveloperEnrollmentReviewStatus Status,
    DeveloperEnrollmentRequestSnapshot? Request = null,
    string? ErrorDetail = null);

/// <summary>
/// List outcome for developer enrollment requests.
/// </summary>
internal sealed record DeveloperEnrollmentRequestListResult(
    DeveloperEnrollmentRequestListStatus Status,
    IReadOnlyList<DeveloperEnrollmentRequestSnapshot>? Requests = null);

/// <summary>
/// Supported moderator review decisions.
/// </summary>
internal enum DeveloperEnrollmentReviewDecision
{
    Approve,
    Reject
}

/// <summary>
/// Moderator list status codes.
/// </summary>
internal enum DeveloperEnrollmentRequestListStatus
{
    Success,
    Forbidden
}

/// <summary>
/// Moderator review status codes.
/// </summary>
internal enum DeveloperEnrollmentReviewStatus
{
    Success,
    Forbidden,
    NotFound,
    Conflict,
    UpstreamFailure
}
