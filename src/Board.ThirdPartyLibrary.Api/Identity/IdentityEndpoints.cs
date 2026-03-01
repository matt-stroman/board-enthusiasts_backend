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

            return Results.Ok(new AuthenticationCallbackResponse(
                AccessToken: exchangeResult.AccessToken,
                RefreshToken: exchangeResult.RefreshToken,
                IdToken: exchangeResult.IdToken,
                TokenType: exchangeResult.TokenType ?? JwtBearerDefaults.AuthenticationScheme,
                ExpiresInSeconds: exchangeResult.ExpiresInSeconds,
                Scope: exchangeResult.Scope,
                User: BuildCurrentUserResponse(ReadClaims(exchangeResult.AccessToken))));
        });

        group.MapGet("/me", [Authorize] (ClaimsPrincipal user) =>
            Results.Ok(BuildCurrentUserResponse(user.Claims)));

        return app;
    }

    private static CurrentUserResponse BuildCurrentUserResponse(IEnumerable<Claim> claims)
    {
        var claimList = claims.ToList();

        return new CurrentUserResponse(
            Subject: GetClaimValue(claimList, "sub") ?? string.Empty,
            DisplayName: GetClaimValue(claimList, "name") ?? GetClaimValue(claimList, "preferred_username") ?? string.Empty,
            Email: GetClaimValue(claimList, "email"),
            EmailVerified: bool.TryParse(GetClaimValue(claimList, "email_verified"), out var emailVerified) && emailVerified,
            IdentityProvider: GetClaimValue(claimList, "identity_provider") ?? GetClaimValue(claimList, "idp"),
            Roles: claimList
                .Where(claim => claim.Type == ClaimTypes.Role)
                .Select(claim => claim.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static IEnumerable<Claim> ReadClaims(string accessToken) =>
        new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims;

    private static string? GetClaimValue(IEnumerable<Claim> claims, string type) =>
        claims.FirstOrDefault(claim => string.Equals(claim.Type, type, StringComparison.OrdinalIgnoreCase))?.Value;
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
