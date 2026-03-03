using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Auth;
using Board.ThirdPartyLibrary.Api.Persistence;
using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Board.ThirdPartyLibrary.Api.Identity;

/// <summary>
/// Persistence contract for the application-owned user projection and Board profile linkage.
/// </summary>
internal interface IIdentityPersistenceService
{
    /// <summary>
    /// Ensures the current authenticated caller exists in the local user projection table.
    /// </summary>
    /// <param name="claims">Authenticated caller claims.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task EnsureCurrentUserProjectionAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the linked Board profile for the current caller when one exists.
    /// </summary>
    /// <param name="claims">Authenticated caller claims.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task<BoardProfileSnapshot?> GetBoardProfileAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates the linked Board profile for the current caller.
    /// </summary>
    /// <param name="claims">Authenticated caller claims.</param>
    /// <param name="command">Normalized Board profile values.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task<BoardProfileSnapshot> UpsertBoardProfileAsync(
        IEnumerable<Claim> claims,
        UpsertBoardProfileCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the linked Board profile for the current caller.
    /// </summary>
    /// <param name="claims">Authenticated caller claims.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task<bool> DeleteBoardProfileAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);
}

/// <summary>
/// Entity Framework-backed implementation of <see cref="IIdentityPersistenceService" />.
/// </summary>
internal sealed class IdentityPersistenceService(BoardLibraryDbContext dbContext) : IIdentityPersistenceService
{
    /// <inheritdoc />
    public async Task EnsureCurrentUserProjectionAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        await EnsureUserAsync(claims, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BoardProfileSnapshot?> GetBoardProfileAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        var user = await EnsureUserAsync(claims, cancellationToken);
        var profile = await dbContext.UserBoardProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.UserId == user.Id, cancellationToken);

        return profile is null ? null : MapSnapshot(profile);
    }

    /// <inheritdoc />
    public async Task<BoardProfileSnapshot> UpsertBoardProfileAsync(
        IEnumerable<Claim> claims,
        UpsertBoardProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await EnsureUserAsync(claims, cancellationToken);
        var now = DateTime.UtcNow;
        var profile = await dbContext.UserBoardProfiles
            .SingleOrDefaultAsync(candidate => candidate.UserId == user.Id, cancellationToken);

        if (profile is null)
        {
            profile = new UserBoardProfile
            {
                UserId = user.Id,
                BoardUserId = command.BoardUserId,
                DisplayName = command.DisplayName,
                AvatarUrl = command.AvatarUrl,
                LinkedAtUtc = now,
                LastSyncedAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.UserBoardProfiles.Add(profile);
        }
        else
        {
            profile.BoardUserId = command.BoardUserId;
            profile.DisplayName = command.DisplayName;
            profile.AvatarUrl = command.AvatarUrl;
            profile.LastSyncedAtUtc = now;
            profile.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapSnapshot(profile);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteBoardProfileAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        var user = await EnsureUserAsync(claims, cancellationToken);
        var profile = await dbContext.UserBoardProfiles
            .SingleOrDefaultAsync(candidate => candidate.UserId == user.Id, cancellationToken);

        if (profile is null)
        {
            return false;
        }

        dbContext.UserBoardProfiles.Remove(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<AppUser> EnsureUserAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        var snapshot = BuildSnapshot(claims);
        var user = await dbContext.Users
            .SingleOrDefaultAsync(candidate => candidate.KeycloakSubject == snapshot.Subject, cancellationToken);

        if (user is null)
        {
            user = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = snapshot.Subject,
                DisplayName = snapshot.DisplayName,
                Email = snapshot.Email,
                EmailVerified = snapshot.EmailVerified,
                IdentityProvider = snapshot.IdentityProvider,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            dbContext.Users.Add(user);
        }
        else
        {
            user.DisplayName = snapshot.DisplayName;
            user.Email = snapshot.Email;
            user.EmailVerified = snapshot.EmailVerified;
            user.IdentityProvider = snapshot.IdentityProvider;
            user.UpdatedAtUtc = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    private static UserSnapshot BuildSnapshot(IEnumerable<Claim> claims)
    {
        var claimList = claims.ToList();
        var subject = ClaimValueResolver.GetSubject(claimList);

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new InvalidOperationException("Authenticated user is missing the required subject claim.");
        }

        return new UserSnapshot(
            Subject: subject,
            DisplayName: ClaimValueResolver.GetClaimValue(claimList, "name") ?? ClaimValueResolver.GetClaimValue(claimList, "preferred_username"),
            Email: ClaimValueResolver.GetClaimValue(claimList, "email"),
            EmailVerified: bool.TryParse(ClaimValueResolver.GetClaimValue(claimList, "email_verified"), out var emailVerified) && emailVerified,
            IdentityProvider: ClaimValueResolver.GetClaimValue(claimList, "identity_provider") ?? ClaimValueResolver.GetClaimValue(claimList, "idp"));
    }

    private static BoardProfileSnapshot MapSnapshot(UserBoardProfile profile) =>
        new(
            profile.BoardUserId,
            profile.DisplayName,
            profile.AvatarUrl,
            profile.LinkedAtUtc,
            profile.LastSyncedAtUtc);
}

/// <summary>
/// Command payload for linking or refreshing a Board profile.
/// </summary>
/// <param name="BoardUserId">Board user identifier.</param>
/// <param name="DisplayName">Board display name.</param>
/// <param name="AvatarUrl">Optional Board avatar URL.</param>
internal sealed record UpsertBoardProfileCommand(string BoardUserId, string DisplayName, string? AvatarUrl);

/// <summary>
/// Snapshot of a linked Board profile.
/// </summary>
/// <param name="BoardUserId">Board user identifier.</param>
/// <param name="DisplayName">Board display name.</param>
/// <param name="AvatarUrl">Optional Board avatar URL.</param>
/// <param name="LinkedAtUtc">UTC timestamp when the profile was first linked.</param>
/// <param name="LastSyncedAtUtc">UTC timestamp of the most recent profile sync.</param>
internal sealed record BoardProfileSnapshot(
    string BoardUserId,
    string DisplayName,
    string? AvatarUrl,
    DateTime LinkedAtUtc,
    DateTime LastSyncedAtUtc);

/// <summary>
/// Cached authenticated user claim snapshot used to upsert the local user projection.
/// </summary>
/// <param name="Subject">Immutable Keycloak subject identifier.</param>
/// <param name="DisplayName">Cached display name when available.</param>
/// <param name="Email">Cached email address when available.</param>
/// <param name="EmailVerified">Whether the cached email address is verified.</param>
/// <param name="IdentityProvider">Upstream identity provider name when available.</param>
internal sealed record UserSnapshot(
    string Subject,
    string? DisplayName,
    string? Email,
    bool EmailVerified,
    string? IdentityProvider);
