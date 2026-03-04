using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Board.ThirdPartyLibrary.Api.Identity;

/// <summary>
/// Maps identity-related endpoints.
/// </summary>
internal static class IdentityEndpoints
{
    /// <summary>
    /// Maps identity-related endpoints to the application.
    /// </summary>
    /// <param name="app">Route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/identity");

        group.MapGet("/roles", () => Results.Ok(new PlatformRoleListResponse(PlatformRoleCatalog.Roles)));

        group.MapGet("/auth/config", (
            IKeycloakEndpointResolver endpointResolver,
            IOptions<KeycloakOptions> options) =>
        {
            var keycloakOptions = options.Value;

            return Results.Ok(new AuthenticationConfigurationResponse(
                Issuer: endpointResolver.GetIssuerUri().AbsoluteUri,
                AuthorizationEndpoint: endpointResolver.GetAuthorizationEndpointUri().AbsoluteUri,
                TokenEndpoint: endpointResolver.GetTokenEndpointUri().AbsoluteUri,
                LogoutEndpoint: endpointResolver.GetLogoutEndpointUri().AbsoluteUri,
                JwksUri: endpointResolver.GetJwksUri().AbsoluteUri,
                AccountManagementUrl: endpointResolver.GetAccountManagementUri().AbsoluteUri,
                ClientId: keycloakOptions.ClientId,
                CallbackUrl: endpointResolver.GetCallbackUri().AbsoluteUri,
                Scopes: keycloakOptions.Scopes,
                RegistrationEnabled: true,
                ExternalIdentityProviders: keycloakOptions.ExternalIdentityProviders));
        });

        group.MapGet("/auth/login", (
            string? provider,
            IKeycloakAuthorizationStateStore authorizationStateStore,
            IKeycloakEndpointResolver endpointResolver,
            IOptions<KeycloakOptions> options) =>
        {
            var authorizationState = authorizationStateStore.Create();
            var keycloakOptions = options.Value;
            var parameters = new Dictionary<string, string?>
            {
                ["client_id"] = keycloakOptions.ClientId,
                ["redirect_uri"] = endpointResolver.GetCallbackUri().AbsoluteUri,
                ["response_type"] = "code",
                ["scope"] = string.Join(' ', keycloakOptions.Scopes),
                ["state"] = authorizationState.State,
                ["code_challenge"] = KeycloakPkce.CreateCodeChallenge(authorizationState.CodeVerifier),
                ["code_challenge_method"] = "S256"
            };

            if (!string.IsNullOrWhiteSpace(provider))
            {
                parameters["kc_idp_hint"] = provider;
            }

            var redirectUri = QueryHelpers.AddQueryString(
                endpointResolver.GetAuthorizationEndpointUri().AbsoluteUri,
                parameters!);

            return Results.Redirect(redirectUri);
        });

        group.MapGet("/auth/callback", async (
            string? code,
            string? state,
            string? error,
            string? error_description,
            IKeycloakAuthorizationStateStore authorizationStateStore,
            IKeycloakTokenClient tokenClient,
            IKeycloakEndpointResolver endpointResolver,
            IIdentityPersistenceService identityPersistenceService,
            CancellationToken cancellationToken) =>
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                return Results.Problem(
                    title: "Authentication failed.",
                    detail: error_description is null ? error : $"{error}: {error_description}",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            {
                return Results.Problem(
                    title: "Authentication callback is invalid.",
                    detail: "Both the authorization code and state are required.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (!authorizationStateStore.TryTake(state, out var authorizationState) || authorizationState is null)
            {
                return Results.Problem(
                    title: "Authentication callback is invalid.",
                    detail: "The authorization state was missing, expired, or already used.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var exchangeResult = await tokenClient.ExchangeAuthorizationCodeAsync(
                new KeycloakTokenExchangeRequest(code, authorizationState.CodeVerifier, endpointResolver.GetCallbackUri()),
                cancellationToken);

            if (!exchangeResult.Succeeded || string.IsNullOrWhiteSpace(exchangeResult.AccessToken))
            {
                return Results.Problem(
                    title: "Keycloak token exchange failed.",
                    detail: exchangeResult.ErrorDescription ?? exchangeResult.Error ?? "The token endpoint returned an unknown error.",
                    statusCode: StatusCodes.Status502BadGateway);
            }

            var accessTokenClaims = ReadClaims(exchangeResult.AccessToken).ToArray();
            await identityPersistenceService.EnsureCurrentUserProjectionAsync(accessTokenClaims, cancellationToken);

            return Results.Ok(new AuthenticationCallbackResponse(
                AccessToken: exchangeResult.AccessToken,
                RefreshToken: exchangeResult.RefreshToken,
                IdToken: exchangeResult.IdToken,
                TokenType: exchangeResult.TokenType ?? JwtBearerDefaults.AuthenticationScheme,
                ExpiresInSeconds: exchangeResult.ExpiresInSeconds,
                Scope: exchangeResult.Scope,
                User: BuildCurrentUserResponse(accessTokenClaims)));
        });

        group.MapGet("/me", [Authorize] async (
            ClaimsPrincipal user,
            IIdentityPersistenceService identityPersistenceService,
            CancellationToken cancellationToken) =>
        {
            await identityPersistenceService.EnsureCurrentUserProjectionAsync(user.Claims, cancellationToken);
            return Results.Ok(BuildCurrentUserResponse(user.Claims));
        });

        group.MapGet("/me/developer-enrollment", [Authorize] async (
            ClaimsPrincipal user,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var enrollment = await developerEnrollmentService.GetCurrentEnrollmentAsync(user.Claims, cancellationToken);
            return Results.Ok(new DeveloperEnrollmentResponse(MapDeveloperEnrollment(enrollment)));
        });

        group.MapPost("/me/developer-enrollment", [Authorize] async (
            ClaimsPrincipal user,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var enrollment = await developerEnrollmentService.SubmitEnrollmentAsync(user.Claims, cancellationToken);
            return Results.Ok(new DeveloperEnrollmentResponse(MapDeveloperEnrollment(enrollment)));
        });

        group.MapGet("/me/board-profile", [Authorize] async (
            ClaimsPrincipal user,
            IIdentityPersistenceService identityPersistenceService,
            CancellationToken cancellationToken) =>
        {
            var boardProfile = await identityPersistenceService.GetBoardProfileAsync(user.Claims, cancellationToken);
            return boardProfile is null
                ? CreateProblemResult(
                    StatusCodes.Status404NotFound,
                    "Board profile not linked",
                    "No linked Board profile exists for the current user.",
                    "board_profile_not_linked")
                : Results.Ok(new BoardProfileResponse(new BoardProfile(
                    boardProfile.BoardUserId,
                    boardProfile.DisplayName,
                    boardProfile.AvatarUrl,
                    boardProfile.LinkedAtUtc,
                    boardProfile.LastSyncedAtUtc)));
        });

        group.MapPut("/me/board-profile", [Authorize] async (
            ClaimsPrincipal user,
            UpsertBoardProfileRequest request,
            IIdentityPersistenceService identityPersistenceService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateBoardProfileRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var boardProfile = await identityPersistenceService.UpsertBoardProfileAsync(
                user.Claims,
                new UpsertBoardProfileCommand(
                    request.BoardUserId.Trim(),
                    request.DisplayName.Trim(),
                    request.AvatarUrl?.Trim()),
                cancellationToken);

            return Results.Ok(new BoardProfileResponse(new BoardProfile(
                boardProfile.BoardUserId,
                boardProfile.DisplayName,
                boardProfile.AvatarUrl,
                boardProfile.LinkedAtUtc,
                boardProfile.LastSyncedAtUtc)));
        });

        group.MapDelete("/me/board-profile", [Authorize] async (
            ClaimsPrincipal user,
            IIdentityPersistenceService identityPersistenceService,
            CancellationToken cancellationToken) =>
        {
            var deleted = await identityPersistenceService.DeleteBoardProfileAsync(user.Claims, cancellationToken);
            return deleted
                ? Results.NoContent()
                : CreateProblemResult(
                    StatusCodes.Status404NotFound,
                    "Board profile not linked",
                    "No linked Board profile exists for the current user.",
                    "board_profile_not_linked");
        });

        return app;
    }

    private static Dictionary<string, string[]> ValidateBoardProfileRequest(UpsertBoardProfileRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(request.BoardUserId))
        {
            errors["boardUserId"] = ["Board user ID is required."];
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            errors["displayName"] = ["Display name is required."];
        }

        if (!string.IsNullOrWhiteSpace(request.AvatarUrl) &&
            !Uri.TryCreate(request.AvatarUrl, UriKind.Absolute, out _))
        {
            errors["avatarUrl"] = ["Avatar URL must be an absolute URI."];
        }

        return errors;
    }

    private static CurrentUserResponse BuildCurrentUserResponse(IEnumerable<Claim> claims)
    {
        var claimList = claims.ToList();

        return new CurrentUserResponse(
            Subject: ClaimValueResolver.GetSubject(claimList) ?? string.Empty,
            DisplayName: ClaimValueResolver.GetClaimValue(claimList, "name") ?? ClaimValueResolver.GetClaimValue(claimList, "preferred_username") ?? string.Empty,
            Email: ClaimValueResolver.GetClaimValue(claimList, "email"),
            EmailVerified: bool.TryParse(ClaimValueResolver.GetClaimValue(claimList, "email_verified"), out var emailVerified) && emailVerified,
            IdentityProvider: ClaimValueResolver.GetClaimValue(claimList, "identity_provider") ?? ClaimValueResolver.GetClaimValue(claimList, "idp"),
            Roles: claimList
                .Where(claim => claim.Type == ClaimTypes.Role)
                .Select(claim => claim.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static IEnumerable<Claim> ReadClaims(string accessToken) =>
        new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims;

    private static DeveloperEnrollment MapDeveloperEnrollment(DeveloperEnrollmentStateSnapshot snapshot) =>
        new(
            snapshot.RequestId,
            snapshot.Status,
            snapshot.DeveloperAccessEnabled,
            snapshot.CanSubmitRequest,
            snapshot.RequestedAtUtc,
            snapshot.ReviewedAtUtc,
            snapshot.ReviewerSubject);

    private static IResult CreateProblemResult(int statusCode, string title, string detail, string code) =>
        Results.Json(
            new ProblemEnvelope(
                Type: $"https://boardtpl.dev/problems/{code.Replace('_', '-')}",
                Title: title,
                Status: statusCode,
                Detail: detail,
                Code: code),
            statusCode: statusCode);
}

/// <summary>
/// Response for the platform roles endpoint.
/// </summary>
/// <param name="Roles">Supported platform roles.</param>
internal sealed record PlatformRoleListResponse(IReadOnlyList<PlatformRoleDefinition> Roles);

/// <summary>
/// Response describing Keycloak-backed authentication settings for clients.
/// </summary>
/// <param name="Issuer">OIDC issuer URI.</param>
/// <param name="AuthorizationEndpoint">OIDC authorization endpoint URI.</param>
/// <param name="TokenEndpoint">OIDC token endpoint URI.</param>
/// <param name="LogoutEndpoint">OIDC logout endpoint URI.</param>
/// <param name="JwksUri">OIDC JWK set URI.</param>
/// <param name="AccountManagementUrl">Keycloak account console URL.</param>
/// <param name="ClientId">Configured client identifier.</param>
/// <param name="CallbackUrl">Backend callback URI.</param>
/// <param name="Scopes">Scopes requested by the backend login flow.</param>
/// <param name="RegistrationEnabled">Whether self-registration is enabled in the realm.</param>
/// <param name="ExternalIdentityProviders">Configured external identity provider aliases.</param>
internal sealed record AuthenticationConfigurationResponse(
    string Issuer,
    string AuthorizationEndpoint,
    string TokenEndpoint,
    string LogoutEndpoint,
    string JwksUri,
    string AccountManagementUrl,
    string ClientId,
    string CallbackUrl,
    IReadOnlyList<string> Scopes,
    bool RegistrationEnabled,
    IReadOnlyList<string> ExternalIdentityProviders);

/// <summary>
/// Response returned after a successful Keycloak authorization-code callback.
/// </summary>
/// <param name="AccessToken">OIDC access token.</param>
/// <param name="RefreshToken">Optional refresh token.</param>
/// <param name="IdToken">Optional ID token.</param>
/// <param name="TokenType">OAuth token type.</param>
/// <param name="ExpiresInSeconds">Access token lifetime in seconds.</param>
/// <param name="Scope">Granted scope string.</param>
/// <param name="User">Resolved user profile derived from the access token.</param>
internal sealed record AuthenticationCallbackResponse(
    string AccessToken,
    string? RefreshToken,
    string? IdToken,
    string TokenType,
    int ExpiresInSeconds,
    string? Scope,
    CurrentUserResponse User);

/// <summary>
/// Current authenticated user summary.
/// </summary>
/// <param name="Subject">Stable identity subject identifier.</param>
/// <param name="DisplayName">Display name or preferred username.</param>
/// <param name="Email">Primary email address.</param>
/// <param name="EmailVerified">Whether the email address is verified.</param>
/// <param name="IdentityProvider">External identity provider alias when applicable.</param>
/// <param name="Roles">Assigned platform roles.</param>
internal sealed record CurrentUserResponse(
    string Subject,
    string DisplayName,
    string? Email,
    bool EmailVerified,
    string? IdentityProvider,
    IReadOnlyList<string> Roles);

/// <summary>
/// Developer-enrollment state for the current user.
/// </summary>
/// <param name="RequestId">Application-owned request identifier when one exists.</param>
/// <param name="Status">Player-facing enrollment status.</param>
/// <param name="DeveloperAccessEnabled">Whether developer access is enabled in Keycloak.</param>
/// <param name="CanSubmitRequest">Whether the caller can still submit a request.</param>
/// <param name="RequestedAt">UTC timestamp when the request was submitted.</param>
/// <param name="ReviewedAt">UTC timestamp when the request was reviewed.</param>
/// <param name="ReviewerSubject">Reviewer Keycloak subject when the request was reviewed.</param>
internal sealed record DeveloperEnrollment(
    Guid? RequestId,
    string Status,
    bool DeveloperAccessEnabled,
    bool CanSubmitRequest,
    DateTime? RequestedAt,
    DateTime? ReviewedAt,
    string? ReviewerSubject);

/// <summary>
/// Response wrapper for developer enrollment.
/// </summary>
/// <param name="DeveloperEnrollment">Developer enrollment result.</param>
internal sealed record DeveloperEnrollmentResponse(DeveloperEnrollment DeveloperEnrollment);

/// <summary>
/// Linked Board profile summary for the current user.
/// </summary>
/// <param name="BoardUserId">Board-owned user identifier.</param>
/// <param name="DisplayName">Board display name.</param>
/// <param name="AvatarUrl">Board avatar image URL.</param>
/// <param name="LinkedAt">UTC timestamp when the profile was first linked.</param>
/// <param name="LastSyncedAt">UTC timestamp when the cached profile was last updated.</param>
internal sealed record BoardProfile(
    string BoardUserId,
    string DisplayName,
    string? AvatarUrl,
    DateTime LinkedAt,
    DateTime LastSyncedAt);

/// <summary>
/// Response wrapper for the linked Board profile endpoint.
/// </summary>
/// <param name="BoardProfile">Current user's linked Board profile.</param>
internal sealed record BoardProfileResponse(BoardProfile BoardProfile);

/// <summary>
/// Request payload for linking or updating a Board profile.
/// </summary>
/// <param name="BoardUserId">Board-owned user identifier.</param>
/// <param name="DisplayName">Board display name.</param>
/// <param name="AvatarUrl">Optional avatar image URL.</param>
internal sealed record UpsertBoardProfileRequest(string BoardUserId, string DisplayName, string? AvatarUrl);

internal sealed record ProblemEnvelope(string? Type, string Title, int Status, string? Detail, string? Code);
