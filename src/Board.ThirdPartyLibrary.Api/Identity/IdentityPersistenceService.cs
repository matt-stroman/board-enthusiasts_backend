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
    /// Gets the current caller's application-managed profile details.
    /// </summary>
    /// <param name="claims">Authenticated caller claims.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task<UserProfileSnapshot> GetCurrentUserProfileAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the current caller's application-managed profile details.
    /// </summary>
    /// <param name="claims">Authenticated caller claims.</param>
    /// <param name="command">Normalized profile values.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task<UserProfileSnapshot> UpdateCurrentUserProfileAsync(
        IEnumerable<Claim> claims,
        UpdateUserProfileCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Configures the current caller avatar to use a hosted URL.
    /// </summary>
    /// <param name="claims">Authenticated caller claims.</param>
    /// <param name="avatarUrl">Absolute avatar URL.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task<UserProfileSnapshot> SetCurrentUserAvatarUrlAsync(
        IEnumerable<Claim> claims,
        string avatarUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads an avatar image for the current caller.
    /// </summary>
    /// <param name="claims">Authenticated caller claims.</param>
    /// <param name="command">Uploaded avatar payload.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task<UserProfileSnapshot> UploadCurrentUserAvatarAsync(
        IEnumerable<Claim> claims,
        UploadedAvatarCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes any configured avatar URL or uploaded avatar for the current caller.
    /// </summary>
    /// <param name="claims">Authenticated caller claims.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task<UserProfileSnapshot> RemoveCurrentUserAvatarAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);

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
    public async Task<UserProfileSnapshot> GetCurrentUserProfileAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        var user = await EnsureUserAsync(claims, cancellationToken);
        return MapUserProfile(user);
    }

    /// <inheritdoc />
    public async Task<UserProfileSnapshot> UpdateCurrentUserProfileAsync(
        IEnumerable<Claim> claims,
        UpdateUserProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await EnsureUserAsync(claims, cancellationToken);

        user.DisplayName = command.DisplayName;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapUserProfile(user);
    }

    /// <inheritdoc />
    public async Task<UserProfileSnapshot> SetCurrentUserAvatarUrlAsync(
        IEnumerable<Claim> claims,
        string avatarUrl,
        CancellationToken cancellationToken = default)
    {
        var user = await EnsureUserAsync(claims, cancellationToken);

        user.AvatarUrl = avatarUrl;
        user.AvatarImageContentType = null;
        user.AvatarImageData = null;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapUserProfile(user);
    }

    /// <inheritdoc />
    public async Task<UserProfileSnapshot> UploadCurrentUserAvatarAsync(
        IEnumerable<Claim> claims,
        UploadedAvatarCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await EnsureUserAsync(claims, cancellationToken);

        user.AvatarUrl = null;
        user.AvatarImageContentType = command.ContentType;
        user.AvatarImageData = command.Content;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapUserProfile(user);
    }

    /// <inheritdoc />
    public async Task<UserProfileSnapshot> RemoveCurrentUserAvatarAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        var user = await EnsureUserAsync(claims, cancellationToken);

        user.AvatarUrl = null;
        user.AvatarImageContentType = null;
        user.AvatarImageData = null;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapUserProfile(user);
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
                UserName = snapshot.UserName,
                FirstName = snapshot.FirstName,
                LastName = snapshot.LastName,
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
            user.DisplayName ??= snapshot.DisplayName;
            user.UserName = snapshot.UserName;
            user.FirstName = snapshot.FirstName;
            user.LastName = snapshot.LastName;
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
            UserName: ClaimValueResolver.GetClaimValue(claimList, "preferred_username"),
            FirstName: ClaimValueResolver.GetClaimValue(claimList, "given_name"),
            LastName: ClaimValueResolver.GetClaimValue(claimList, "family_name"),
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

    private static UserProfileSnapshot MapUserProfile(AppUser user) =>
        new(
            user.KeycloakSubject,
            user.DisplayName,
            user.UserName,
            user.FirstName,
            user.LastName,
            user.Email,
            user.EmailVerified,
            user.AvatarUrl,
            user.AvatarImageData is not null && !string.IsNullOrWhiteSpace(user.AvatarImageContentType)
                ? new UploadedAvatarSnapshot(user.AvatarImageContentType, user.AvatarImageData)
                : null,
            BuildInitials(user.FirstName, user.LastName, user.DisplayName, user.UserName),
            user.UpdatedAtUtc);

    private static string BuildInitials(string? firstName, string? lastName, string? displayName, string? userName)
    {
        static string? GetLetter(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            foreach (var character in value.Trim())
            {
                if (char.IsLetterOrDigit(character))
                {
                    return char.ToUpperInvariant(character).ToString();
                }
            }

            return null;
        }

        var firstLetter = GetLetter(firstName);
        var lastLetter = GetLetter(lastName);
        if (firstLetter is not null || lastLetter is not null)
        {
            return string.Concat(firstLetter ?? string.Empty, lastLetter ?? string.Empty);
        }

        var displayParts = (displayName ?? userName ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (displayParts.Length == 0)
        {
            return "U";
        }

        if (displayParts.Length == 1)
        {
            return GetLetter(displayParts[0]) ?? "U";
        }

        return string.Concat(GetLetter(displayParts[0]) ?? string.Empty, GetLetter(displayParts[^1]) ?? string.Empty);
    }
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
/// Command payload for updating the current user's application-managed profile details.
/// </summary>
/// <param name="DisplayName">Display name used by the application.</param>
internal sealed record UpdateUserProfileCommand(string? DisplayName);

/// <summary>
/// Command payload for uploaded avatar data.
/// </summary>
/// <param name="ContentType">Normalized uploaded avatar MIME type.</param>
/// <param name="Content">Uploaded avatar binary content.</param>
internal sealed record UploadedAvatarCommand(string ContentType, byte[] Content);

/// <summary>
/// Snapshot of uploaded avatar data stored for a user.
/// </summary>
/// <param name="ContentType">Avatar image MIME type.</param>
/// <param name="Content">Avatar image content bytes.</param>
internal sealed record UploadedAvatarSnapshot(string ContentType, byte[] Content);

/// <summary>
/// Snapshot of the current user's application-managed profile details.
/// </summary>
/// <param name="Subject">Immutable Keycloak subject identifier.</param>
/// <param name="DisplayName">Display name used by the application.</param>
/// <param name="UserName">Username sourced from Keycloak claims.</param>
/// <param name="FirstName">First name sourced from Keycloak claims.</param>
/// <param name="LastName">Last name sourced from Keycloak claims.</param>
/// <param name="Email">Cached email address from identity claims.</param>
/// <param name="EmailVerified">Cached email verification flag from identity claims.</param>
/// <param name="AvatarUrl">Hosted avatar URL when configured.</param>
/// <param name="UploadedAvatar">Uploaded avatar image when configured.</param>
/// <param name="Initials">Two-letter fallback avatar initials.</param>
/// <param name="UpdatedAtUtc">UTC timestamp when the profile was last updated.</param>
internal sealed record UserProfileSnapshot(
    string Subject,
    string? DisplayName,
    string? UserName,
    string? FirstName,
    string? LastName,
    string? Email,
    bool EmailVerified,
    string? AvatarUrl,
    UploadedAvatarSnapshot? UploadedAvatar,
    string Initials,
    DateTime UpdatedAtUtc);

/// <summary>
/// Cached authenticated user claim snapshot used to upsert the local user projection.
/// </summary>
/// <param name="Subject">Immutable Keycloak subject identifier.</param>
/// <param name="DisplayName">Cached display name when available.</param>
/// <param name="UserName">Cached username when available.</param>
/// <param name="FirstName">Cached first name when available.</param>
/// <param name="LastName">Cached last name when available.</param>
/// <param name="Email">Cached email address when available.</param>
/// <param name="EmailVerified">Whether the cached email address is verified.</param>
/// <param name="IdentityProvider">Upstream identity provider name when available.</param>
internal sealed record UserSnapshot(
    string Subject,
    string? DisplayName,
    string? UserName,
    string? FirstName,
    string? LastName,
    string? Email,
    bool EmailVerified,
    string? IdentityProvider);
