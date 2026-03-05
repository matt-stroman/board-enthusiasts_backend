using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Board.ThirdPartyLibrary.Api.Auth;
using Board.ThirdPartyLibrary.Api.HealthChecks;
using Board.ThirdPartyLibrary.Api.Identity;
using Board.ThirdPartyLibrary.Api.Persistence;
using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Board.ThirdPartyLibrary.Api.Tests;

/// <summary>
/// Endpoint tests for the minimal API surface exposed by the backend service.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ApiEndpointTests
{
    /// <summary>
    /// Verifies the root endpoint returns service metadata and known health endpoints.
    /// </summary>
    [Fact]
    public async Task RootEndpoint_ReturnsServiceMetadata()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        Assert.Equal("board-third-party-lib-backend", root.GetProperty("service").GetString());
        Assert.Contains(
            root.GetProperty("endpoints").EnumerateArray().Select(element => element.GetString()),
            endpoint => endpoint == "/health/live");
        Assert.Contains(
            root.GetProperty("endpoints").EnumerateArray().Select(element => element.GetString()),
            endpoint => endpoint == "/health/ready");
    }

    /// <summary>
    /// Verifies the liveness endpoint reports healthy without dependency checks.
    /// </summary>
    [Fact]
    public async Task LiveHealthEndpoint_ReturnsHealthyWithoutDependencyChecks()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.Equal("Healthy", root.GetProperty("status").GetString());
        Assert.Empty(root.GetProperty("checks").EnumerateArray());
    }

    /// <summary>
    /// Verifies liveness remains healthy even if the readiness probe would fail.
    /// </summary>
    [Fact]
    public async Task LiveHealthEndpoint_IgnoresReadinessProbeFailures()
    {
        using var factory = new TestApiFactory(
            _ => throw new InvalidOperationException("Probe should not be invoked by liveness endpoint."));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("Healthy", document.RootElement.GetProperty("status").GetString());
    }

    /// <summary>
    /// Verifies readiness reports healthy and includes database/user metadata on success.
    /// </summary>
    [Fact]
    public async Task ReadyHealthEndpoint_WhenProbeSucceeds_ReturnsHealthy()
    {
        using var factory = new TestApiFactory(
            _ => Task.FromResult(PostgresReadinessProbeResult.Healthy("board_tpl", "board_tpl_user")));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.Equal("Healthy", root.GetProperty("status").GetString());

        var postgresCheck = root.GetProperty("checks")
            .EnumerateArray()
            .Single(check => check.GetProperty("name").GetString() == "postgres");

        Assert.Equal("Healthy", postgresCheck.GetProperty("status").GetString());
        Assert.Equal(
            "board_tpl",
            postgresCheck.GetProperty("data").GetProperty("database").GetString());
        Assert.Equal(
            "board_tpl_user",
            postgresCheck.GetProperty("data").GetProperty("user").GetString());
    }

    /// <summary>
    /// Verifies readiness returns service unavailable when the probe reports an unhealthy result.
    /// </summary>
    [Fact]
    public async Task ReadyHealthEndpoint_WhenProbeReportsUnhealthy_ReturnsServiceUnavailable()
    {
        using var factory = new TestApiFactory(
            _ => Task.FromResult(PostgresReadinessProbeResult.Unhealthy("Configured test failure.")));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.Equal("Unhealthy", root.GetProperty("status").GetString());

        var postgresCheck = root.GetProperty("checks")
            .EnumerateArray()
            .Single(check => check.GetProperty("name").GetString() == "postgres");

        Assert.Equal("Unhealthy", postgresCheck.GetProperty("status").GetString());
        Assert.Equal("Configured test failure.", postgresCheck.GetProperty("description").GetString());
    }

    /// <summary>
    /// Verifies readiness returns service unavailable when the probe throws unexpectedly.
    /// </summary>
    [Fact]
    public async Task ReadyHealthEndpoint_WhenProbeThrows_ReturnsServiceUnavailable()
    {
        using var factory = new TestApiFactory(
            _ => throw new InvalidOperationException("Boom"));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.Equal("Unhealthy", root.GetProperty("status").GetString());

        var postgresCheck = root.GetProperty("checks")
            .EnumerateArray()
            .Single(check => check.GetProperty("name").GetString() == "postgres");

        Assert.Equal("Unhealthy", postgresCheck.GetProperty("status").GetString());
        Assert.Equal("PostgreSQL connection failed.", postgresCheck.GetProperty("description").GetString());
    }

    /// <summary>
    /// Verifies the platform roles endpoint returns the seeded role catalog.
    /// </summary>
    [Fact]
    public async Task PlatformRolesEndpoint_ReturnsExpectedRoles()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/identity/roles");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var roles = document.RootElement.GetProperty("roles").EnumerateArray().ToList();

        Assert.Equal(4, roles.Count);
        Assert.Contains(roles, role => role.GetProperty("code").GetString() == "player");
        Assert.Contains(roles, role => role.GetProperty("code").GetString() == "developer");
        Assert.Contains(roles, role => role.GetProperty("code").GetString() == "admin");
        Assert.Contains(roles, role => role.GetProperty("code").GetString() == "moderator");
    }

    /// <summary>
    /// Verifies the auth config endpoint advertises Keycloak metadata needed by clients.
    /// </summary>
    [Fact]
    public async Task AuthenticationConfigurationEndpoint_ReturnsKeycloakMetadata()
    {
        using var factory = new TestApiFactory(
            configureConfiguration: configurationBuilder =>
            {
                configurationBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Authentication:Keycloak:BaseUrl"] = "https://localhost:8443",
                        ["Authentication:Keycloak:Realm"] = "board-third-party-library",
                        ["Authentication:Keycloak:ClientId"] = "board-third-party-library-backend",
                        ["Authentication:Keycloak:PublicBackendBaseUrl"] = "https://localhost:7085",
                        ["Authentication:Keycloak:ExternalIdentityProviders:0"] = "github",
                        ["Authentication:Keycloak:ExternalIdentityProviders:1"] = "google"
                    });
            });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/identity/auth/config");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.Equal("https://localhost:8443/realms/board-third-party-library/", root.GetProperty("issuer").GetString());
        Assert.Equal("board-third-party-library-backend", root.GetProperty("clientId").GetString());
        Assert.Equal("https://localhost:7085/identity/auth/callback", root.GetProperty("callbackUrl").GetString());
        Assert.Contains(
            root.GetProperty("externalIdentityProviders").EnumerateArray().Select(element => element.GetString()),
            provider => provider == "github");
    }

    /// <summary>
    /// Verifies the login endpoint redirects callers to Keycloak with PKCE and provider hint values.
    /// </summary>
    [Fact]
    public async Task AuthenticationLoginEndpoint_RedirectsToKeycloakAuthorizationEndpoint()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/identity/auth/login?provider=github");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var location = response.Headers.Location!;
        var query = ParseQuery(location.Query);

        Assert.Equal("https://localhost:8443/realms/board-third-party-library/protocol/openid-connect/auth", location.GetLeftPart(UriPartial.Path));
        Assert.Equal("board-third-party-library-backend", query["client_id"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal("github", query["kc_idp_hint"]);
        Assert.False(string.IsNullOrWhiteSpace(query["state"]));
        Assert.False(string.IsNullOrWhiteSpace(query["code_challenge"]));
    }

    /// <summary>
    /// Verifies callback requests without a valid state are rejected.
    /// </summary>
    [Fact]
    public async Task AuthenticationCallbackEndpoint_WithUnknownState_ReturnsBadRequest()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/identity/auth/callback?code=abc123&state=missing");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("Authentication callback is invalid.", document.RootElement.GetProperty("title").GetString());
    }

    /// <summary>
    /// Verifies callback requests return tokens and the resolved user profile after a successful exchange.
    /// </summary>
    [Fact]
    public async Task AuthenticationCallbackEndpoint_WhenExchangeSucceeds_ReturnsTokensAndUserProfile()
    {
        const string state = "known-state";
        const string codeVerifier = "known-code-verifier";

        using var factory = new TestApiFactory(
            configureServices: services =>
            {
                services.RemoveAll<IKeycloakAuthorizationStateStore>();
                services.AddSingleton<IKeycloakAuthorizationStateStore>(
                    new StubAuthorizationStateStore(new KeycloakAuthorizationState(state, codeVerifier)));
                services.RemoveAll<IKeycloakTokenClient>();
                services.AddSingleton<IKeycloakTokenClient>(
                    new StubKeycloakTokenClient(
                        KeycloakTokenExchangeResult.Success(
                            accessToken: CreateAccessToken(
                                new Claim("sub", "user-123"),
                                new Claim("name", "Local Admin"),
                                new Claim("email", "admin@boardtpl.local"),
                                new Claim("email_verified", "true"),
                                new Claim(ClaimTypes.Role, "admin"),
                                new Claim(ClaimTypes.Role, "developer")),
                            tokenType: "Bearer",
                            expiresInSeconds: 300,
                            scope: "openid profile email",
                            refreshToken: "refresh-token",
                            idToken: "id-token")));
            });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/identity/auth/callback?code=abc123&state={state}");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.Equal("refresh-token", root.GetProperty("refreshToken").GetString());
        Assert.Equal("Bearer", root.GetProperty("tokenType").GetString());
        Assert.Equal("Local Admin", root.GetProperty("user").GetProperty("displayName").GetString());
        Assert.Equal("admin@boardtpl.local", root.GetProperty("user").GetProperty("email").GetString());
        Assert.Contains(
            root.GetProperty("user").GetProperty("roles").EnumerateArray().Select(element => element.GetString()),
            role => role == "admin");
    }

    /// <summary>
    /// Verifies callback requests return a gateway error when the token exchange fails upstream.
    /// </summary>
    [Fact]
    public async Task AuthenticationCallbackEndpoint_WhenExchangeFails_ReturnsBadGateway()
    {
        const string state = "known-state";

        using var factory = new TestApiFactory(
            configureServices: services =>
            {
                services.RemoveAll<IKeycloakAuthorizationStateStore>();
                services.AddSingleton<IKeycloakAuthorizationStateStore>(
                    new StubAuthorizationStateStore(new KeycloakAuthorizationState(state, "known-code-verifier")));
                services.RemoveAll<IKeycloakTokenClient>();
                services.AddSingleton<IKeycloakTokenClient>(
                    new StubKeycloakTokenClient(
                        KeycloakTokenExchangeResult.Failure("invalid_grant", "The authorization code is invalid.")));
            });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/identity/auth/callback?code=abc123&state={state}");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("Keycloak token exchange failed.", document.RootElement.GetProperty("title").GetString());
    }

    /// <summary>
    /// Verifies the current-user endpoint requires authentication.
    /// </summary>
    [Fact]
    public async Task CurrentUserEndpoint_WithoutBearerToken_ReturnsUnauthorized()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/identity/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Verifies the current-user endpoint returns claims from the authenticated principal.
    /// </summary>
    [Fact]
    public async Task CurrentUserEndpoint_WithAuthenticatedUser_ReturnsProfile()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim("name", "Local Admin"),
                new Claim("email", "admin@boardtpl.local"),
                new Claim("email_verified", "true"),
                new Claim(ClaimTypes.Role, "admin"),
                new Claim(ClaimTypes.Role, "developer")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/identity/me");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.Equal("user-123", root.GetProperty("subject").GetString());
        Assert.Equal("Local Admin", root.GetProperty("displayName").GetString());
        Assert.Equal("admin@boardtpl.local", root.GetProperty("email").GetString());
        Assert.True(root.GetProperty("emailVerified").GetBoolean());
        Assert.Contains(
            root.GetProperty("roles").EnumerateArray().Select(element => element.GetString()),
            role => role == "admin");
    }

    /// <summary>
    /// Verifies the current-user endpoint accepts the framework-mapped nameidentifier claim as the user subject.
    /// </summary>
    [Fact]
    public async Task CurrentUserEndpoint_WithMappedSubjectClaim_ReturnsProfile()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim(ClaimTypes.NameIdentifier, "user-123"),
                new Claim("name", "Local Admin"),
                new Claim("email", "admin@boardtpl.local"),
                new Claim("email_verified", "true"),
                new Claim(ClaimTypes.Role, "developer")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/identity/me");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.Equal("user-123", root.GetProperty("subject").GetString());
        Assert.Equal("Local Admin", root.GetProperty("displayName").GetString());
        Assert.Equal("admin@boardtpl.local", root.GetProperty("email").GetString());
        Assert.True(root.GetProperty("emailVerified").GetBoolean());
    }

    /// <summary>
    /// Verifies the current-user endpoint creates or updates the local user projection.
    /// </summary>
    [Fact]
    public async Task CurrentUserEndpoint_WithAuthenticatedUser_PersistsUserProjection()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim("name", "Local Admin"),
                new Claim("email", "admin@boardtpl.local"),
                new Claim("email_verified", "true"),
                new Claim("idp", "google"),
                new Claim(ClaimTypes.Role, "admin")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/identity/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var user = await dbContext.Users.SingleAsync(candidate => candidate.KeycloakSubject == "user-123");

        Assert.Equal("Local Admin", user.DisplayName);
        Assert.Equal("admin@boardtpl.local", user.Email);
        Assert.True(user.EmailVerified);
        Assert.Equal("google", user.IdentityProvider);
    }

    /// <summary>
    /// Verifies the current-user profile endpoint requires authentication.
    /// </summary>
    [Fact]
    public async Task CurrentUserProfileEndpoint_WithoutBearerToken_ReturnsUnauthorized()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/identity/me/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Verifies current-user profile details return seeded fallback initials and claim-derived values.
    /// </summary>
    [Fact]
    public async Task CurrentUserProfileEndpoint_WithAuthenticatedUser_ReturnsProfile()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim("name", "Local Admin"),
                new Claim("preferred_username", "local-admin"),
                new Claim("given_name", "Local"),
                new Claim("family_name", "Admin"),
                new Claim("email", "admin@boardtpl.local"),
                new Claim("email_verified", "true"),
                new Claim(ClaimTypes.Role, "player")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/identity/me/profile");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var profile = document.RootElement.GetProperty("profile");
        Assert.Equal("user-123", profile.GetProperty("subject").GetString());
        Assert.Equal("local-admin", profile.GetProperty("userName").GetString());
        Assert.Equal("Local", profile.GetProperty("firstName").GetString());
        Assert.Equal("Admin", profile.GetProperty("lastName").GetString());
        Assert.Equal("LA", profile.GetProperty("initials").GetString());
    }

    /// <summary>
    /// Verifies invalid profile update payloads are rejected.
    /// </summary>
    [Fact]
    public async Task UpdateCurrentUserProfileEndpoint_WithInvalidPayload_ReturnsUnprocessableEntity()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim("name", "Local Admin"),
                new Claim(ClaimTypes.Role, "player")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/identity/me/profile",
            new
            {
                displayName = new string('d', 201)
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode)StatusCodes.Status422UnprocessableEntity, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var errors = document.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("displayName", out _));
    }

    /// <summary>
    /// Verifies profile updates persist and return normalized values.
    /// </summary>
    [Fact]
    public async Task UpdateCurrentUserProfileEndpoint_WithAuthenticatedUser_PersistsAndReturnsProfile()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim("name", "Local Admin"),
                new Claim("preferred_username", "local-admin"),
                new Claim("given_name", "Local"),
                new Claim("family_name", "Admin"),
                new Claim(ClaimTypes.Role, "player")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/identity/me/profile",
            new
            {
                displayName = "Board Enthusiast"
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var profile = document.RootElement.GetProperty("profile");
        Assert.Equal("Board Enthusiast", profile.GetProperty("displayName").GetString());
        Assert.Equal("local-admin", profile.GetProperty("userName").GetString());
        Assert.Equal("Local", profile.GetProperty("firstName").GetString());
        Assert.Equal("Admin", profile.GetProperty("lastName").GetString());
        Assert.Equal("LA", profile.GetProperty("initials").GetString());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var user = await dbContext.Users.SingleAsync(candidate => candidate.KeycloakSubject == "user-123");
        Assert.Equal("local-admin", user.UserName);
        Assert.Equal("Local", user.FirstName);
        Assert.Equal("Admin", user.LastName);
    }

    /// <summary>
    /// Verifies hosted avatar URL updates persist and clear uploaded avatar data.
    /// </summary>
    [Fact]
    public async Task SetAvatarUrlEndpoint_WithAuthenticatedUser_PersistsAvatarUrl()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim("name", "Local Admin"),
                new Claim(ClaimTypes.Role, "player")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/identity/me/profile/avatar-url",
            new
            {
                avatarUrl = "https://cdn.example.com/avatar.png"
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var profile = document.RootElement.GetProperty("profile");
        Assert.Equal("https://cdn.example.com/avatar.png", profile.GetProperty("avatarUrl").GetString());
        Assert.False(profile.TryGetProperty("avatarDataUrl", out _));
    }

    /// <summary>
    /// Verifies uploaded avatars are persisted and returned as data URLs.
    /// </summary>
    [Fact]
    public async Task UploadAvatarEndpoint_WithValidImage_ReturnsDataUrl()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim("name", "Local Admin"),
                new Claim(ClaimTypes.Role, "player")
            ]);
        using var client = factory.CreateClient();

        var avatarContent = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
        avatarContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        using var content = new MultipartFormDataContent
        {
            { avatarContent, "Avatar", "avatar.png" }
        };

        using var response = await client.PostAsync("/identity/me/profile/avatar-upload", content);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var profile = document.RootElement.GetProperty("profile");
        var avatarDataUrl = profile.GetProperty("avatarDataUrl").GetString();
        Assert.NotNull(avatarDataUrl);
        Assert.StartsWith("data:image/png;base64,", avatarDataUrl, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies avatar removal clears any configured avatar values.
    /// </summary>
    [Fact]
    public async Task RemoveAvatarEndpoint_WithExistingAvatar_ClearsAvatarValues()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim("name", "Local Admin"),
                new Claim(ClaimTypes.Role, "player")
            ]);
        using var client = factory.CreateClient();

        using var setUrlResponse = await client.PutAsJsonAsync(
            "/identity/me/profile/avatar-url",
            new
            {
                avatarUrl = "https://cdn.example.com/avatar.png"
            });
        Assert.Equal(HttpStatusCode.OK, setUrlResponse.StatusCode);

        using var deleteResponse = await client.DeleteAsync("/identity/me/profile/avatar");
        var payload = await deleteResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var profile = document.RootElement.GetProperty("profile");
        Assert.False(profile.TryGetProperty("avatarUrl", out _));
        Assert.False(profile.TryGetProperty("avatarDataUrl", out _));
    }

    /// <summary>
    /// Verifies the developer-enrollment status endpoint requires authentication.
    /// </summary>
    [Fact]
    public async Task DeveloperEnrollmentStatusEndpoint_WithoutBearerToken_ReturnsUnauthorized()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/identity/me/developer-enrollment");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Verifies players without an existing request see the not-requested state.
    /// </summary>
    [Fact]
    public async Task DeveloperEnrollmentStatusEndpoint_WithoutExistingRequest_ReturnsNotRequested()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim("name", "Player One"),
                new Claim(ClaimTypes.Role, "player")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/identity/me/developer-enrollment");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var enrollment = document.RootElement.GetProperty("developerEnrollment");
        Assert.Equal("not_requested", enrollment.GetProperty("status").GetString());
        Assert.False(enrollment.GetProperty("developerAccessEnabled").GetBoolean());
        Assert.True(enrollment.GetProperty("canSubmitRequest").GetBoolean());
    }

    /// <summary>
    /// Verifies callers who already have developer access receive an approved response.
    /// </summary>
    [Fact]
    public async Task DeveloperEnrollmentEndpoint_WithExistingDeveloperRole_ReturnsAlreadyEnabled()
    {
        var roleClient = new StubKeycloakUserRoleClient(KeycloakUserRoleAssignmentResult.Success(alreadyAssigned: true));

        using var factory = new TestApiFactory(
            configureServices: services =>
            {
                services.RemoveAll<IKeycloakUserRoleClient>();
                services.AddSingleton<IKeycloakUserRoleClient>(roleClient);
            },
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim("name", "Local Developer"),
                new Claim(ClaimTypes.Role, "developer")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/identity/me/developer-enrollment", null);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var enrollment = document.RootElement.GetProperty("developerEnrollment");
        Assert.Equal("approved", enrollment.GetProperty("status").GetString());
        Assert.True(enrollment.GetProperty("developerAccessEnabled").GetBoolean());
        Assert.False(enrollment.GetProperty("canSubmitRequest").GetBoolean());
        Assert.Equal(0, roleClient.CallCount);
    }

    /// <summary>
    /// Verifies player-only callers create a pending request through the backend endpoint.
    /// </summary>
    [Fact]
    public async Task DeveloperEnrollmentEndpoint_WithPlayerRole_CreatesPendingRequest()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim("name", "Player One"),
                new Claim("email", "player@boardtpl.local"),
                new Claim("email_verified", "true"),
                new Claim(ClaimTypes.Role, "player")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/identity/me/developer-enrollment", null);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var enrollment = document.RootElement.GetProperty("developerEnrollment");
        Assert.Equal("pending_review", enrollment.GetProperty("status").GetString());
        Assert.False(enrollment.GetProperty("developerAccessEnabled").GetBoolean());
        Assert.False(enrollment.GetProperty("canSubmitRequest").GetBoolean());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var user = await dbContext.Users.SingleAsync(candidate => candidate.KeycloakSubject == "user-123");
        var request = await dbContext.DeveloperEnrollmentRequests.SingleAsync(candidate => candidate.UserId == user.Id);

        Assert.Equal("Player One", user.DisplayName);
        Assert.Equal(DeveloperEnrollmentStatuses.Pending, request.Status);
    }

    /// <summary>
    /// Verifies rejected requests remain rejected instead of being recreated.
    /// </summary>
    [Fact]
    public async Task DeveloperEnrollmentEndpoint_WithRejectedRequest_ReturnsRejected()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim("name", "Player One"),
                new Claim(ClaimTypes.Role, "player")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "user-123",
                DisplayName = "Player One",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            dbContext.Users.Add(user);
            var thread = new ConversationThread
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            };

            dbContext.ConversationThreads.Add(thread);
            dbContext.DeveloperEnrollmentRequests.Add(new DeveloperEnrollmentRequest
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Status = DeveloperEnrollmentStatuses.Rejected,
                ConversationThreadId = thread.Id,
                ConversationThread = thread,
                RequestedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                ReviewedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                ReapplyAvailableAtUtc = DateTime.UtcNow.AddDays(30),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/identity/me/developer-enrollment", null);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var enrollment = document.RootElement.GetProperty("developerEnrollment");
        Assert.Equal("rejected", enrollment.GetProperty("status").GetString());
        Assert.False(enrollment.GetProperty("developerAccessEnabled").GetBoolean());
        Assert.False(enrollment.GetProperty("canSubmitRequest").GetBoolean());
    }

    /// <summary>
    /// Verifies moderators can list developer enrollment requests.
    /// </summary>
    [Fact]
    public async Task ModeratorEnrollmentListEndpoint_WithModeratorRole_ReturnsRequests()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "moderator-123"),
                new Claim("name", "Moderator User"),
                new Claim(ClaimTypes.Role, "moderator")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            var moderator = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "moderator-123",
                DisplayName = "Moderator User",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var applicant = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "player-123",
                DisplayName = "Player One",
                Email = "player@boardtpl.local",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            dbContext.Users.AddRange(moderator, applicant);
            var thread = new ConversationThread
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            };

            dbContext.ConversationThreads.Add(thread);
            dbContext.DeveloperEnrollmentRequests.Add(new DeveloperEnrollmentRequest
            {
                Id = Guid.Parse("2c54f9bb-1fdf-48e5-8cf0-a8b77f6174af"),
                UserId = applicant.Id,
                Status = DeveloperEnrollmentStatuses.Pending,
                ConversationThreadId = thread.Id,
                ConversationThread = thread,
                RequestedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/moderation/developer-enrollment-requests");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var requests = document.RootElement.GetProperty("requests").EnumerateArray().ToList();
        Assert.Single(requests);
        Assert.Equal("player-123", requests[0].GetProperty("applicantSubject").GetString());
        Assert.Equal("pending_review", requests[0].GetProperty("status").GetString());
    }

    /// <summary>
    /// Verifies non-moderators cannot list developer enrollment requests.
    /// </summary>
    [Fact]
    public async Task ModeratorEnrollmentListEndpoint_WithoutModeratorRole_ReturnsForbidden()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "player-123"),
                new Claim(ClaimTypes.Role, "player")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/moderation/developer-enrollment-requests");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("moderator_access_required", document.RootElement.GetProperty("code").GetString());
    }

    /// <summary>
    /// Verifies moderators can approve pending developer enrollment requests.
    /// </summary>
    [Fact]
    public async Task ModeratorApproveEnrollmentEndpoint_WithPendingRequest_ApprovesAndAssignsRole()
    {
        var roleClient = new StubKeycloakUserRoleClient(KeycloakUserRoleAssignmentResult.Success(alreadyAssigned: false));

        using var factory = new TestApiFactory(
            configureServices: services =>
            {
                services.RemoveAll<IKeycloakUserRoleClient>();
                services.AddSingleton<IKeycloakUserRoleClient>(roleClient);
            },
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "moderator-123"),
                new Claim("name", "Moderator User"),
                new Claim(ClaimTypes.Role, "moderator")
            ]);

        var requestId = Guid.Parse("2c54f9bb-1fdf-48e5-8cf0-a8b77f6174af");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            var moderator = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "moderator-123",
                DisplayName = "Moderator User",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var applicant = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "player-123",
                DisplayName = "Player One",
                Email = "player@boardtpl.local",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            dbContext.Users.AddRange(moderator, applicant);
            var thread = new ConversationThread
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            };

            dbContext.ConversationThreads.Add(thread);
            dbContext.DeveloperEnrollmentRequests.Add(new DeveloperEnrollmentRequest
            {
                Id = requestId,
                UserId = applicant.Id,
                Status = DeveloperEnrollmentStatuses.Pending,
                ConversationThreadId = thread.Id,
                ConversationThread = thread,
                RequestedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsync($"/moderation/developer-enrollment-requests/{requestId}/approve", null);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var request = document.RootElement.GetProperty("developerEnrollmentRequest");
        Assert.Equal("approved", request.GetProperty("status").GetString());
        Assert.True(request.GetProperty("developerAccessEnabled").GetBoolean());
        Assert.Equal(1, roleClient.CallCount);
        Assert.Equal("player-123", roleClient.LastUserSubject);

        using var approvalScope = factory.Services.CreateScope();
        var approvalDbContext = approvalScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var persisted = await approvalDbContext.DeveloperEnrollmentRequests.SingleAsync(candidate => candidate.Id == requestId);
        Assert.Equal(DeveloperEnrollmentStatuses.Approved, persisted.Status);
        Assert.NotNull(persisted.ReviewedAtUtc);
        Assert.NotNull(persisted.ReviewedByUserId);
    }

    /// <summary>
    /// Verifies Keycloak approval failures are returned as bad gateway responses.
    /// </summary>
    [Fact]
    public async Task ModeratorApproveEnrollmentEndpoint_WhenKeycloakRoleAssignmentFails_ReturnsBadGateway()
    {
        using var factory = new TestApiFactory(
            configureServices: services =>
            {
                services.RemoveAll<IKeycloakUserRoleClient>();
                services.AddSingleton<IKeycloakUserRoleClient>(
                    new StubKeycloakUserRoleClient(
                        KeycloakUserRoleAssignmentResult.Failure("Keycloak role assignment failed for the authenticated user.")));
            },
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "moderator-123"),
                new Claim(ClaimTypes.Role, "moderator")
            ]);

        var requestId = Guid.Parse("2c54f9bb-1fdf-48e5-8cf0-a8b77f6174af");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            var moderator = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "moderator-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var applicant = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "player-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            dbContext.Users.AddRange(moderator, applicant);
            var thread = new ConversationThread
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            };

            dbContext.ConversationThreads.Add(thread);
            dbContext.DeveloperEnrollmentRequests.Add(new DeveloperEnrollmentRequest
            {
                Id = requestId,
                UserId = applicant.Id,
                Status = DeveloperEnrollmentStatuses.Pending,
                ConversationThreadId = thread.Id,
                ConversationThread = thread,
                RequestedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsync($"/moderation/developer-enrollment-requests/{requestId}/approve", null);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("keycloak_developer_enrollment_failed", document.RootElement.GetProperty("code").GetString());
    }

    /// <summary>
    /// Verifies moderators can request more information on a pending enrollment request.
    /// </summary>
    [Fact]
    public async Task ModeratorRequestMoreInformationEndpoint_WithPendingRequest_ReturnsAwaitingApplicantResponse()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "moderator-123"),
                new Claim("name", "Moderator User"),
                new Claim(ClaimTypes.Role, "moderator")
            ]);

        var requestId = Guid.Parse("2c54f9bb-1fdf-48e5-8cf0-a8b77f6174af");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            var moderator = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "moderator-123",
                DisplayName = "Moderator User",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var applicant = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "player-123",
                DisplayName = "Player One",
                Email = "player@boardtpl.local",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var thread = new ConversationThread
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            };

            dbContext.Users.AddRange(moderator, applicant);
            dbContext.ConversationThreads.Add(thread);
            dbContext.DeveloperEnrollmentRequests.Add(new DeveloperEnrollmentRequest
            {
                Id = requestId,
                UserId = applicant.Id,
                Status = DeveloperEnrollmentStatuses.Pending,
                ConversationThreadId = thread.Id,
                ConversationThread = thread,
                RequestedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var content = new MultipartFormDataContent
        {
            { new StringContent("Please share links to prior shipped work."), "message" }
        };

        using var response = await client.PostAsync($"/moderation/developer-enrollment-requests/{requestId}/request-more-information", content);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var request = document.RootElement.GetProperty("developerEnrollmentRequest");
        Assert.Equal("awaiting_applicant_response", request.GetProperty("status").GetString());

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var persisted = await verificationDbContext.DeveloperEnrollmentRequests
            .Include(candidate => candidate.ConversationThread)
            .ThenInclude(thread => thread.Messages)
            .SingleAsync(candidate => candidate.Id == requestId);

        Assert.Equal(DeveloperEnrollmentStatuses.AwaitingApplicantResponse, persisted.Status);
        Assert.Single(persisted.ConversationThread.Messages);
    }

    /// <summary>
    /// Verifies applicants can reply after a moderator requests more information.
    /// </summary>
    [Fact]
    public async Task DeveloperEnrollmentReplyEndpoint_WithAwaitingRequest_ReturnsPendingReview()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "player-123"),
                new Claim("name", "Player One"),
                new Claim(ClaimTypes.Role, "player")
            ]);

        var requestId = Guid.Parse("2c54f9bb-1fdf-48e5-8cf0-a8b77f6174af");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            var applicant = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "player-123",
                DisplayName = "Player One",
                Email = "player@boardtpl.local",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var moderator = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "moderator-123",
                DisplayName = "Moderator User",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var thread = new ConversationThread
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            };

            dbContext.Users.AddRange(applicant, moderator);
            dbContext.ConversationThreads.Add(thread);
            dbContext.DeveloperEnrollmentRequests.Add(new DeveloperEnrollmentRequest
            {
                Id = requestId,
                UserId = applicant.Id,
                Status = DeveloperEnrollmentStatuses.AwaitingApplicantResponse,
                ConversationThreadId = thread.Id,
                ConversationThread = thread,
                RequestedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                LastModeratorActionByUserId = moderator.Id,
                LastModeratorActionAtUtc = DateTime.UtcNow.AddMinutes(-5),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var content = new MultipartFormDataContent
        {
            { new StringContent("Here are the details you asked for."), "message" }
        };

        using var response = await client.PostAsync($"/identity/me/developer-enrollment/{requestId}/messages", content);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var enrollment = document.RootElement.GetProperty("developerEnrollment");
        Assert.Equal("pending_review", enrollment.GetProperty("status").GetString());
    }

    /// <summary>
    /// Verifies applicants can cancel an open enrollment request.
    /// </summary>
    [Fact]
    public async Task DeveloperEnrollmentCancelEndpoint_WithOpenRequest_ReturnsCancelled()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "player-123"),
                new Claim("name", "Player One"),
                new Claim(ClaimTypes.Role, "player")
            ]);

        var requestId = Guid.Parse("2c54f9bb-1fdf-48e5-8cf0-a8b77f6174af");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            var applicant = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "player-123",
                DisplayName = "Player One",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var thread = new ConversationThread
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            };

            dbContext.Users.Add(applicant);
            dbContext.ConversationThreads.Add(thread);
            dbContext.DeveloperEnrollmentRequests.Add(new DeveloperEnrollmentRequest
            {
                Id = requestId,
                UserId = applicant.Id,
                Status = DeveloperEnrollmentStatuses.Pending,
                ConversationThreadId = thread.Id,
                ConversationThread = thread,
                RequestedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsync($"/identity/me/developer-enrollment/{requestId}/cancel", null);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var enrollment = document.RootElement.GetProperty("developerEnrollment");
        Assert.Equal("cancelled", enrollment.GetProperty("status").GetString());
        Assert.True(enrollment.GetProperty("canSubmitRequest").GetBoolean());
    }

    /// <summary>
    /// Verifies moderators can reject pending developer enrollment requests with a probation window.
    /// </summary>
    [Fact]
    public async Task ModeratorRejectEnrollmentEndpoint_WithPendingRequest_ReturnsRejectedAndProbation()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "moderator-123"),
                new Claim("name", "Moderator User"),
                new Claim(ClaimTypes.Role, "moderator")
            ]);

        var requestId = Guid.Parse("2c54f9bb-1fdf-48e5-8cf0-a8b77f6174af");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            var moderator = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "moderator-123",
                DisplayName = "Moderator User",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var applicant = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "player-123",
                DisplayName = "Player One",
                Email = "player@boardtpl.local",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var thread = new ConversationThread
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            };

            dbContext.Users.AddRange(moderator, applicant);
            dbContext.ConversationThreads.Add(thread);
            dbContext.DeveloperEnrollmentRequests.Add(new DeveloperEnrollmentRequest
            {
                Id = requestId,
                UserId = applicant.Id,
                Status = DeveloperEnrollmentStatuses.Pending,
                ConversationThreadId = thread.Id,
                ConversationThread = thread,
                RequestedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var content = new MultipartFormDataContent
        {
            { new StringContent("Your prior work samples were not sufficient."), "message" }
        };

        using var response = await client.PostAsync($"/moderation/developer-enrollment-requests/{requestId}/reject", content);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var request = document.RootElement.GetProperty("developerEnrollmentRequest");
        Assert.Equal("rejected", request.GetProperty("status").GetString());
        Assert.NotNull(request.GetProperty("reapplyAvailableAt").GetString());

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var persisted = await verificationDbContext.DeveloperEnrollmentRequests
            .SingleAsync(candidate => candidate.Id == requestId);

        Assert.Equal(DeveloperEnrollmentStatuses.Rejected, persisted.Status);
        Assert.NotNull(persisted.ReapplyAvailableAtUtc);
    }

    /// <summary>
    /// Verifies moderator rejection requires comments.
    /// </summary>
    [Fact]
    public async Task ModeratorRejectEnrollmentEndpoint_WithoutComments_ReturnsUnprocessableEntity()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "moderator-123"),
                new Claim(ClaimTypes.Role, "moderator")
            ]);

        var requestId = Guid.Parse("2c54f9bb-1fdf-48e5-8cf0-a8b77f6174af");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            var moderator = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "moderator-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var applicant = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "player-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var thread = new ConversationThread
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            };

            dbContext.Users.AddRange(moderator, applicant);
            dbContext.ConversationThreads.Add(thread);
            dbContext.DeveloperEnrollmentRequests.Add(new DeveloperEnrollmentRequest
            {
                Id = requestId,
                UserId = applicant.Id,
                Status = DeveloperEnrollmentStatuses.Pending,
                ConversationThreadId = thread.Id,
                ConversationThread = thread,
                RequestedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var content = new MultipartFormDataContent
        {
            { new StringContent("   "), "message" }
        };
        using var response = await client.PostAsync($"/moderation/developer-enrollment-requests/{requestId}/reject", content);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode)StatusCodes.Status422UnprocessableEntity, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("message", out _));
    }

    /// <summary>
    /// Verifies applicants can load their own developer enrollment conversation history.
    /// </summary>
    [Fact]
    public async Task DeveloperEnrollmentConversationEndpoint_WithApplicantRequest_ReturnsConversation()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "player-123"),
                new Claim("name", "Player One"),
                new Claim(ClaimTypes.Role, "player")
            ]);

        var requestId = Guid.Parse("2c54f9bb-1fdf-48e5-8cf0-a8b77f6174af");
        var attachmentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            var applicant = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "player-123",
                DisplayName = "Player One",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var moderator = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "moderator-123",
                DisplayName = "Moderator User",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var thread = new ConversationThread
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            };
            var message = new ConversationMessage
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                Thread = thread,
                UserId = moderator.Id,
                User = moderator,
                AuthorRole = ConversationAuthorRoles.Moderator,
                MessageKind = ConversationMessageKinds.ModeratorInformationRequest,
                Body = "Please send prior release notes.",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                Attachments =
                [
                    new ConversationMessageAttachment
                    {
                        Id = attachmentId,
                        FileName = "checklist.txt",
                        ContentType = "text/plain",
                        SizeBytes = 5,
                        Content = "hello"u8.ToArray(),
                        CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                        UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
                    }
                ]
            };

            thread.Messages.Add(message);
            dbContext.Users.AddRange(applicant, moderator);
            dbContext.ConversationThreads.Add(thread);
            dbContext.DeveloperEnrollmentRequests.Add(new DeveloperEnrollmentRequest
            {
                Id = requestId,
                UserId = applicant.Id,
                Status = DeveloperEnrollmentStatuses.AwaitingApplicantResponse,
                ConversationThreadId = thread.Id,
                ConversationThread = thread,
                RequestedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10)
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/identity/me/developer-enrollment/{requestId}/conversation");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var conversation = document.RootElement.GetProperty("conversation");
        var messages = conversation.GetProperty("messages").EnumerateArray().ToList();
        Assert.Single(messages);
        Assert.Equal("moderator_information_request", messages[0].GetProperty("messageKind").GetString());
        var attachments = messages[0].GetProperty("attachments").EnumerateArray().ToList();
        Assert.Single(attachments);
        Assert.Equal(attachmentId.ToString(), attachments[0].GetProperty("attachmentId").GetString());
    }

    /// <summary>
    /// Verifies moderators can load enrollment conversations.
    /// </summary>
    [Fact]
    public async Task ModeratorEnrollmentConversationEndpoint_WithModeratorRole_ReturnsConversation()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "moderator-123"),
                new Claim("name", "Moderator User"),
                new Claim(ClaimTypes.Role, "moderator")
            ]);

        var requestId = Guid.Parse("2c54f9bb-1fdf-48e5-8cf0-a8b77f6174af");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            var applicant = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "player-123",
                DisplayName = "Player One",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var moderator = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "moderator-123",
                DisplayName = "Moderator User",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var thread = new ConversationThread
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            };

            thread.Messages.Add(new ConversationMessage
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                Thread = thread,
                UserId = applicant.Id,
                User = applicant,
                AuthorRole = ConversationAuthorRoles.Applicant,
                MessageKind = ConversationMessageKinds.ApplicantReply,
                Body = "Here are the requested details.",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-2)
            });
            dbContext.Users.AddRange(applicant, moderator);
            dbContext.ConversationThreads.Add(thread);
            dbContext.DeveloperEnrollmentRequests.Add(new DeveloperEnrollmentRequest
            {
                Id = requestId,
                UserId = applicant.Id,
                Status = DeveloperEnrollmentStatuses.Pending,
                ConversationThreadId = thread.Id,
                ConversationThread = thread,
                RequestedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-2),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10)
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/moderation/developer-enrollment-requests/{requestId}/conversation");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("pending_review", document.RootElement.GetProperty("conversation").GetProperty("status").GetString());
    }

    /// <summary>
    /// Verifies applicants can download their own enrollment attachments.
    /// </summary>
    [Fact]
    public async Task DeveloperEnrollmentAttachmentEndpoint_WithApplicantRequest_ReturnsFile()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "player-123"),
                new Claim(ClaimTypes.Role, "player")
            ]);

        var requestId = Guid.Parse("2c54f9bb-1fdf-48e5-8cf0-a8b77f6174af");
        var attachmentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            var applicant = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "player-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var moderator = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "moderator-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var thread = new ConversationThread
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            };
            thread.Messages.Add(new ConversationMessage
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                Thread = thread,
                UserId = moderator.Id,
                User = moderator,
                AuthorRole = ConversationAuthorRoles.Moderator,
                MessageKind = ConversationMessageKinds.ModeratorInformationRequest,
                Body = "Attachment included.",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-4),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-4),
                Attachments =
                [
                    new ConversationMessageAttachment
                    {
                        Id = attachmentId,
                        FileName = "checklist.txt",
                        ContentType = "text/plain",
                        SizeBytes = 5,
                        Content = "hello"u8.ToArray(),
                        CreatedAtUtc = DateTime.UtcNow.AddMinutes(-4),
                        UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-4)
                    }
                ]
            });

            dbContext.Users.AddRange(applicant, moderator);
            dbContext.ConversationThreads.Add(thread);
            dbContext.DeveloperEnrollmentRequests.Add(new DeveloperEnrollmentRequest
            {
                Id = requestId,
                UserId = applicant.Id,
                Status = DeveloperEnrollmentStatuses.AwaitingApplicantResponse,
                ConversationThreadId = thread.Id,
                ConversationThread = thread,
                RequestedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-4),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/identity/me/developer-enrollment/{requestId}/attachments/{attachmentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("hello", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Verifies moderators can download enrollment attachments.
    /// </summary>
    [Fact]
    public async Task ModeratorEnrollmentAttachmentEndpoint_WithModeratorRole_ReturnsFile()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "moderator-123"),
                new Claim(ClaimTypes.Role, "moderator")
            ]);

        var requestId = Guid.Parse("2c54f9bb-1fdf-48e5-8cf0-a8b77f6174af");
        var attachmentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            var applicant = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "player-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var moderator = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "moderator-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var thread = new ConversationThread
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            };
            thread.Messages.Add(new ConversationMessage
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                Thread = thread,
                UserId = applicant.Id,
                User = applicant,
                AuthorRole = ConversationAuthorRoles.Applicant,
                MessageKind = ConversationMessageKinds.ApplicantReply,
                Body = "Please review this file.",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-4),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-4),
                Attachments =
                [
                    new ConversationMessageAttachment
                    {
                        Id = attachmentId,
                        FileName = "reply.txt",
                        ContentType = "text/plain",
                        SizeBytes = 2,
                        Content = "ok"u8.ToArray(),
                        CreatedAtUtc = DateTime.UtcNow.AddMinutes(-4),
                        UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-4)
                    }
                ]
            });

            dbContext.Users.AddRange(applicant, moderator);
            dbContext.ConversationThreads.Add(thread);
            dbContext.DeveloperEnrollmentRequests.Add(new DeveloperEnrollmentRequest
            {
                Id = requestId,
                UserId = applicant.Id,
                Status = DeveloperEnrollmentStatuses.Pending,
                ConversationThreadId = thread.Id,
                ConversationThread = thread,
                RequestedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-4),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/moderation/developer-enrollment-requests/{requestId}/attachments/{attachmentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Verifies authenticated users can list in-app notifications.
    /// </summary>
    [Fact]
    public async Task NotificationsEndpoint_WithAuthenticatedUser_ReturnsNotifications()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "player-123"),
                new Claim("name", "Player One"),
                new Claim(ClaimTypes.Role, "player")
            ]);

        var unreadId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var readId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "player-123",
                DisplayName = "Player One",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            dbContext.Users.Add(user);
            dbContext.UserNotifications.AddRange(
                new UserNotification
                {
                    Id = readId,
                    UserId = user.Id,
                    Category = NotificationCategories.DeveloperEnrollment,
                    Title = "Older read notification",
                    Body = "Read already.",
                    IsRead = true,
                    ReadAtUtc = DateTime.UtcNow.AddMinutes(-5),
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                    UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
                },
                new UserNotification
                {
                    Id = unreadId,
                    UserId = user.Id,
                    Category = NotificationCategories.DeveloperEnrollment,
                    Title = "Unread notification",
                    Body = "Still unread.",
                    IsRead = false,
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                    UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
                });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/identity/me/notifications");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var notifications = document.RootElement.GetProperty("notifications").EnumerateArray().ToList();
        Assert.Equal(2, notifications.Count);
        Assert.Equal(unreadId.ToString(), notifications[0].GetProperty("notificationId").GetString());
        Assert.False(notifications[0].GetProperty("isRead").GetBoolean());
    }

    /// <summary>
    /// Verifies marking a notification read updates the stored notification state.
    /// </summary>
    [Fact]
    public async Task MarkNotificationReadEndpoint_WithExistingNotification_ReturnsReadNotification()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "player-123"),
                new Claim(ClaimTypes.Role, "player")
            ]);

        var notificationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "player-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            dbContext.Users.Add(user);
            dbContext.UserNotifications.Add(new UserNotification
            {
                Id = notificationId,
                UserId = user.Id,
                Category = NotificationCategories.DeveloperEnrollment,
                Title = "Unread notification",
                Body = "Still unread.",
                IsRead = false,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsync($"/identity/me/notifications/{notificationId}/read", null);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var notification = document.RootElement.GetProperty("notification");
        Assert.True(notification.GetProperty("isRead").GetBoolean());
        Assert.NotNull(notification.GetProperty("readAt").GetString());
    }

    /// <summary>
    /// Verifies the linked Board profile endpoint requires authentication.
    /// </summary>
    [Fact]
    public async Task BoardProfileEndpoint_WithoutBearerToken_ReturnsUnauthorized()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/identity/me/board-profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Verifies a missing Board profile returns a not-found problem payload.
    /// </summary>
    [Fact]
    public async Task BoardProfileEndpoint_WhenProfileIsMissing_ReturnsNotFound()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim("preferred_username", "local-admin")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/identity/me/board-profile");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("board_profile_not_linked", document.RootElement.GetProperty("code").GetString());
    }

    /// <summary>
    /// Verifies invalid Board profile payloads are rejected.
    /// </summary>
    [Fact]
    public async Task UpsertBoardProfileEndpoint_WithInvalidPayload_ReturnsUnprocessableEntity()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim("preferred_username", "local-admin")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/identity/me/board-profile",
            new
            {
                boardUserId = "",
                displayName = "",
                avatarUrl = "not-a-valid-uri"
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode)StatusCodes.Status422UnprocessableEntity, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var errors = document.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("boardUserId", out _));
        Assert.True(errors.TryGetProperty("displayName", out _));
        Assert.True(errors.TryGetProperty("avatarUrl", out _));
    }

    /// <summary>
    /// Verifies linking a Board profile persists and returns the stored profile.
    /// </summary>
    [Fact]
    public async Task UpsertBoardProfileEndpoint_WithAuthenticatedUser_PersistsAndReturnsProfile()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim("name", "Local Admin"),
                new Claim("email", "admin@boardtpl.local")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/identity/me/board-profile",
            new
            {
                boardUserId = "board_user_12345",
                displayName = "BoardKiddo",
                avatarUrl = "https://cdn.board.fun/users/board_user_12345/avatar.png"
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var boardProfile = document.RootElement.GetProperty("boardProfile");
        Assert.Equal("board_user_12345", boardProfile.GetProperty("boardUserId").GetString());
        Assert.Equal("BoardKiddo", boardProfile.GetProperty("displayName").GetString());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var user = await dbContext.Users.Include(candidate => candidate.BoardProfile)
            .SingleAsync(candidate => candidate.KeycloakSubject == "user-123");

        Assert.NotNull(user.BoardProfile);
        Assert.Equal("board_user_12345", user.BoardProfile!.BoardUserId);
        Assert.Equal("BoardKiddo", user.BoardProfile.DisplayName);
    }

    /// <summary>
    /// Verifies unlinking a Board profile removes the persisted link.
    /// </summary>
    [Fact]
    public async Task DeleteBoardProfileEndpoint_WhenProfileExists_RemovesProfile()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim("preferred_username", "local-admin")
            ]);
        using var client = factory.CreateClient();

        using var upsertResponse = await client.PutAsJsonAsync(
            "/identity/me/board-profile",
            new
            {
                boardUserId = "board_user_12345",
                displayName = "BoardKiddo",
                avatarUrl = "https://cdn.board.fun/users/board_user_12345/avatar.png"
            });

        Assert.Equal(HttpStatusCode.OK, upsertResponse.StatusCode);

        using var deleteResponse = await client.DeleteAsync("/identity/me/board-profile");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var getResponse = await client.GetAsync("/identity/me/board-profile");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var user = await dbContext.Users.Include(candidate => candidate.BoardProfile)
            .SingleAsync(candidate => candidate.KeycloakSubject == "user-123");

        Assert.Null(user.BoardProfile);
    }

    private static Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                part => Uri.UnescapeDataString(part[0]),
                part => part.Length > 1 ? Uri.UnescapeDataString(part[1]) : string.Empty,
                StringComparer.Ordinal);

    private static string CreateAccessToken(params Claim[] claims)
    {
        var jwt = new JwtSecurityToken(
            issuer: "https://localhost:8443/realms/board-third-party-library",
            audience: "board-third-party-library-backend",
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(5));

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        private readonly Func<CancellationToken, Task<PostgresReadinessProbeResult>>? _probeFunc;
        private readonly Action<IServiceCollection>? _configureServices;
        private readonly Action<IConfigurationBuilder>? _configureConfiguration;
        private readonly bool _useTestAuthentication;
        private readonly IReadOnlyList<Claim> _testClaims;
        private readonly bool _useInMemoryPersistence;
        private readonly string _inMemoryDatabaseName;

        public TestApiFactory(
            Func<CancellationToken, Task<PostgresReadinessProbeResult>>? probeFunc = null,
            Action<IServiceCollection>? configureServices = null,
            Action<IConfigurationBuilder>? configureConfiguration = null,
            bool useTestAuthentication = false,
            IEnumerable<Claim>? testClaims = null,
            bool useInMemoryPersistence = true)
        {
            _probeFunc = probeFunc;
            _configureServices = configureServices;
            _configureConfiguration = configureConfiguration;
            _useTestAuthentication = useTestAuthentication;
            _testClaims = testClaims?.ToList() ?? [];
            _useInMemoryPersistence = useInMemoryPersistence;
            _inMemoryDatabaseName = $"board-third-party-lib-tests-{Guid.NewGuid():N}";
        }

        protected override void ConfigureClient(HttpClient client)
        {
            base.ConfigureClient(client);
            client.DefaultRequestVersion = HttpVersion.Version20;
            client.DefaultVersionPolicy = System.Net.Http.HttpVersionPolicy.RequestVersionOrHigher;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:BoardLibrary"] = "Host=unit-test;Port=5432;Database=board_tpl_tests;Username=board_tpl_test;Password=board_tpl_test"
                    });

                _configureConfiguration?.Invoke(configurationBuilder);
            });

            builder.ConfigureTestServices(services =>
            {
                if (_useInMemoryPersistence)
                {
                    var inMemoryProvider = new ServiceCollection()
                        .AddEntityFrameworkInMemoryDatabase()
                        .BuildServiceProvider();

                    services.RemoveAll<DbContextOptions<BoardLibraryDbContext>>();
                    services.RemoveAll<BoardLibraryDbContext>();
                    services.AddDbContext<BoardLibraryDbContext>(options =>
                        options
                            .UseInMemoryDatabase(_inMemoryDatabaseName)
                            .UseInternalServiceProvider(inMemoryProvider));
                }

                if (_probeFunc is not null)
                {
                    services.RemoveAll<IPostgresReadinessProbe>();
                    services.AddSingleton<IPostgresReadinessProbe>(new FakePostgresReadinessProbe(_probeFunc));
                }

                if (_useTestAuthentication)
                {
                    services.AddSingleton(new TestAuthClaimsProvider(_testClaims));
                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                        options.DefaultScheme = TestAuthHandler.SchemeName;
                    }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                }

                services.RemoveAll<IKeycloakUserRoleClient>();
                services.AddSingleton<IKeycloakUserRoleClient>(
                    new StubKeycloakUserRoleClient(KeycloakUserRoleAssignmentResult.Success(alreadyAssigned: false)));

                _configureServices?.Invoke(services);
            });
        }
    }

    private sealed class FakePostgresReadinessProbe : IPostgresReadinessProbe
    {
        private readonly Func<CancellationToken, Task<PostgresReadinessProbeResult>> _probeFunc;

        public FakePostgresReadinessProbe(Func<CancellationToken, Task<PostgresReadinessProbeResult>> probeFunc)
        {
            _probeFunc = probeFunc;
        }

        public Task<PostgresReadinessProbeResult> ProbeAsync(CancellationToken cancellationToken = default) =>
            _probeFunc(cancellationToken);
    }

    private sealed class StubAuthorizationStateStore : IKeycloakAuthorizationStateStore
    {
        private readonly KeycloakAuthorizationState _state;
        private bool _consumed;

        public StubAuthorizationStateStore(KeycloakAuthorizationState state)
        {
            _state = state;
        }

        public KeycloakAuthorizationState Create() => _state;

        public bool TryTake(string state, out KeycloakAuthorizationState? authorizationState)
        {
            if (!_consumed && string.Equals(state, _state.State, StringComparison.Ordinal))
            {
                _consumed = true;
                authorizationState = _state;
                return true;
            }

            authorizationState = null;
            return false;
        }
    }

    private sealed class StubKeycloakTokenClient : IKeycloakTokenClient
    {
        private readonly KeycloakTokenExchangeResult _result;

        public StubKeycloakTokenClient(KeycloakTokenExchangeResult result)
        {
            _result = result;
        }

        public Task<KeycloakTokenExchangeResult> ExchangeAuthorizationCodeAsync(
            KeycloakTokenExchangeRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_result);
    }

    private sealed class StubKeycloakUserRoleClient : IKeycloakUserRoleClient
    {
        private readonly KeycloakUserRoleAssignmentResult _result;

        public StubKeycloakUserRoleClient(KeycloakUserRoleAssignmentResult result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public string? LastUserSubject { get; private set; }

        public Task<KeycloakUserRoleAssignmentResult> EnsureRealmRoleAssignedAsync(
            string userSubject,
            string roleName,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastUserSubject = userSubject;
            return Task.FromResult(_result);
        }
    }

    private sealed class TestAuthClaimsProvider
    {
        public TestAuthClaimsProvider(IReadOnlyList<Claim> claims)
        {
            Claims = claims;
        }

        public IReadOnlyList<Claim> Claims { get; }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        private readonly TestAuthClaimsProvider _claimsProvider;

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            TestAuthClaimsProvider claimsProvider)
            : base(options, logger, encoder)
        {
            _claimsProvider = claimsProvider;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(_claimsProvider.Claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
