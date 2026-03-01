using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Persistence;
using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Board.ThirdPartyLibrary.Api.Identity;

internal interface IIdentityPersistenceService
{
    Task EnsureCurrentUserProjectionAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);

    Task<BoardProfileSnapshot?> GetBoardProfileAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);

    Task<BoardProfileSnapshot> UpsertBoardProfileAsync(
        IEnumerable<Claim> claims,
        UpsertBoardProfileCommand command,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteBoardProfileAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);
}

internal sealed class IdentityPersistenceService(BoardLibraryDbContext dbContext) : IIdentityPersistenceService
{
    public async Task EnsureCurrentUserProjectionAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        await EnsureUserAsync(claims, cancellationToken);
    }

    public async Task<BoardProfileSnapshot?> GetBoardProfileAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        var user = await EnsureUserAsync(claims, cancellationToken);
        var profile = await dbContext.UserBoardProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.UserId == user.Id, cancellationToken);

        return profile is null ? null : MapSnapshot(profile);
    }

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
        var subject = GetClaimValue(claimList, "sub");

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new InvalidOperationException("Authenticated user is missing the required subject claim.");
        }

        return new UserSnapshot(
            Subject: subject,
            DisplayName: GetClaimValue(claimList, "name") ?? GetClaimValue(claimList, "preferred_username"),
            Email: GetClaimValue(claimList, "email"),
            EmailVerified: bool.TryParse(GetClaimValue(claimList, "email_verified"), out var emailVerified) && emailVerified,
            IdentityProvider: GetClaimValue(claimList, "identity_provider") ?? GetClaimValue(claimList, "idp"));
    }

    private static BoardProfileSnapshot MapSnapshot(UserBoardProfile profile) =>
        new(
            profile.BoardUserId,
            profile.DisplayName,
            profile.AvatarUrl,
            profile.LinkedAtUtc,
            profile.LastSyncedAtUtc);

    private static string? GetClaimValue(IEnumerable<Claim> claims, string type) =>
        claims.FirstOrDefault(claim => string.Equals(claim.Type, type, StringComparison.OrdinalIgnoreCase))?.Value;
}

internal sealed record UpsertBoardProfileCommand(string BoardUserId, string DisplayName, string? AvatarUrl);

internal sealed record BoardProfileSnapshot(
    string BoardUserId,
    string DisplayName,
    string? AvatarUrl,
    DateTime LinkedAtUtc,
    DateTime LastSyncedAtUtc);

internal sealed record UserSnapshot(
    string Subject,
    string? DisplayName,
    string? Email,
    bool EmailVerified,
    string? IdentityProvider);
