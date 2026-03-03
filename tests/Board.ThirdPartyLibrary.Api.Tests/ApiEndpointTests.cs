using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Board.ThirdPartyLibrary.Api.Auth;
using Board.ThirdPartyLibrary.Api.HealthChecks;
using Board.ThirdPartyLibrary.Api.Persistence;
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
