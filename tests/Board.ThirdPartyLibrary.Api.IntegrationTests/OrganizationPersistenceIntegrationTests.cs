using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Board.ThirdPartyLibrary.Api.Persistence;
using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace Board.ThirdPartyLibrary.Api.IntegrationTests;

/// <summary>
/// Integration tests for the Wave 2 organization persistence model and endpoints.
/// </summary>
[Trait("Category", "Integration")]
public sealed class OrganizationPersistenceIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("board_tpl")
        .WithUsername("board_tpl_user")
        .WithPassword("board_tpl_password")
        .Build();

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
    }

    /// <summary>
    /// Verifies organization create, public read, membership listing, and delete all work against real PostgreSQL.
    /// </summary>
    [Fact]
    public async Task OrganizationEndpoints_WithRealPostgres_RoundTripPersistedData()
    {
        await using (var migrationContext = CreateDbContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        using var factory = new RealPostgresApiFactory(
            _postgresContainer.GetConnectionString(),
            [
                new Claim("sub", "owner-123"),
                new Claim("name", "Owner User"),
                new Claim("email", "owner@boardtpl.local"),
                new Claim(ClaimTypes.Role, "developer")
            ]);
        using var client = factory.CreateClient();

        using var createResponse = await client.PostAsJsonAsync(
            "/organizations",
            new
            {
                slug = "stellar-forge",
                displayName = "Stellar Forge",
                description = "Family co-op studio.",
                logoUrl = "https://cdn.example.com/orgs/stellar-forge.png"
            });
        var createPayload = await createResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(createPayload);
        var organizationId = Guid.Parse(createDocument.RootElement.GetProperty("organization").GetProperty("id").GetString()!);

        await using (var seedContext = CreateDbContext())
        {
            var editorUser = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "editor-456",
                DisplayName = "Editor User",
                Email = "editor@boardtpl.local",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            seedContext.Users.Add(editorUser);
            await seedContext.SaveChangesAsync();
        }

        using var membershipResponse = await client.PutAsJsonAsync(
            $"/developer/organizations/{organizationId}/memberships/editor-456",
            new
            {
                role = "editor"
            });

        Assert.Equal(HttpStatusCode.OK, membershipResponse.StatusCode);

        using var publicGetResponse = await client.GetAsync("/organizations/stellar-forge");
        var publicGetPayload = await publicGetResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, publicGetResponse.StatusCode);

        using var publicGetDocument = JsonDocument.Parse(publicGetPayload);
        Assert.Equal(
            "Stellar Forge",
            publicGetDocument.RootElement.GetProperty("organization").GetProperty("displayName").GetString());

        using var membershipsGetResponse = await client.GetAsync($"/developer/organizations/{organizationId}/memberships");
        var membershipsPayload = await membershipsGetResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, membershipsGetResponse.StatusCode);

        using var membershipsDocument = JsonDocument.Parse(membershipsPayload);
        Assert.Equal(2, membershipsDocument.RootElement.GetProperty("memberships").GetArrayLength());

        using var deleteResponse = await client.DeleteAsync($"/developer/organizations/{organizationId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await using var verificationContext = CreateDbContext();
        Assert.False(await verificationContext.Organizations.AnyAsync(candidate => candidate.Id == organizationId));
        Assert.False(await verificationContext.OrganizationMemberships.AnyAsync(candidate => candidate.OrganizationId == organizationId));
    }

    /// <summary>
    /// Verifies the schema rejects duplicate organization slugs.
    /// </summary>
    [Fact]
    public async Task Schema_WithDuplicateOrganizationSlug_RejectsSecondOrganization()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();

        dbContext.Organizations.Add(new Organization
        {
            Id = Guid.NewGuid(),
            Slug = "stellar-forge",
            DisplayName = "Stellar Forge",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();

        dbContext.Organizations.Add(new Organization
        {
            Id = Guid.NewGuid(),
            Slug = "stellar-forge",
            DisplayName = "Different Name",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    /// <summary>
    /// Verifies the schema rejects duplicate memberships for the same organization and user.
    /// </summary>
    [Fact]
    public async Task Schema_WithDuplicateOrganizationMembership_RejectsSecondMembership()
    {
        Guid organizationId;
        Guid userId;

        await using (var dbContext = CreateDbContext())
        {
            await dbContext.Database.MigrateAsync();

            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "owner-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var organization = new Organization
            {
                Id = Guid.NewGuid(),
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            organizationId = organization.Id;
            userId = user.Id;

            dbContext.Users.Add(user);
            dbContext.Organizations.Add(organization);
            await dbContext.SaveChangesAsync();

            dbContext.OrganizationMemberships.Add(new OrganizationMembership
            {
                OrganizationId = organizationId,
                UserId = userId,
                Role = "owner",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync();
        }

        await using var verificationContext = CreateDbContext();
        verificationContext.OrganizationMemberships.Add(new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = "admin",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => verificationContext.SaveChangesAsync());
    }

    /// <summary>
    /// Verifies duplicate organization creation returns the public conflict payload against PostgreSQL.
    /// </summary>
    [Fact]
    public async Task CreateOrganizationEndpoint_WithDuplicateSlug_ReturnsConflict()
    {
        await using (var migrationContext = CreateDbContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        using var factory = new RealPostgresApiFactory(
            _postgresContainer.GetConnectionString(),
            [
                new Claim("sub", "owner-123"),
                new Claim("name", "Owner User"),
                new Claim(ClaimTypes.Role, "developer")
            ]);
        using var client = factory.CreateClient();

        using var firstResponse = await client.PostAsJsonAsync(
            "/organizations",
            new
            {
                slug = "stellar-forge",
                displayName = "Stellar Forge"
            });
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var secondResponse = await client.PostAsJsonAsync(
            "/organizations",
            new
            {
                slug = "stellar-forge",
                displayName = "Duplicate Stellar Forge"
            });
        var payload = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("organization_slug_conflict", document.RootElement.GetProperty("code").GetString());
    }

    /// <summary>
    /// Verifies organization updates return the public conflict payload when a duplicate slug is requested.
    /// </summary>
    [Fact]
    public async Task UpdateOrganizationEndpoint_WithDuplicateSlug_ReturnsConflict()
    {
        await using (var migrationContext = CreateDbContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        var ownerUserId = Guid.NewGuid();
        var firstOrganizationId = Guid.NewGuid();
        var secondOrganizationId = Guid.NewGuid();

        await using (var seedContext = CreateDbContext())
        {
            seedContext.Users.Add(new AppUser
            {
                Id = ownerUserId,
                KeycloakSubject = "owner-123",
                DisplayName = "Owner User",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            seedContext.Organizations.AddRange(
                new Organization
                {
                    Id = firstOrganizationId,
                    Slug = "stellar-forge",
                    DisplayName = "Stellar Forge",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new Organization
                {
                    Id = secondOrganizationId,
                    Slug = "tabletop-sparks",
                    DisplayName = "Tabletop Sparks",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            seedContext.OrganizationMemberships.Add(new OrganizationMembership
            {
                OrganizationId = secondOrganizationId,
                UserId = ownerUserId,
                Role = "owner",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await seedContext.SaveChangesAsync();
        }

        using var factory = new RealPostgresApiFactory(
            _postgresContainer.GetConnectionString(),
            [
                new Claim("sub", "owner-123"),
                new Claim("name", "Owner User"),
                new Claim(ClaimTypes.Role, "developer")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            $"/developer/organizations/{secondOrganizationId}",
            new
            {
                slug = "stellar-forge",
                displayName = "Tabletop Sparks"
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("organization_slug_conflict", document.RootElement.GetProperty("code").GetString());
    }

    private BoardLibraryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BoardLibraryDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        return new BoardLibraryDbContext(options);
    }

    private sealed class RealPostgresApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;
        private readonly IReadOnlyList<Claim> _claims;

        public RealPostgresApiFactory(string connectionString, IReadOnlyList<Claim> claims)
        {
            _connectionString = connectionString;
            _claims = claims;
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
                        ["ConnectionStrings:BoardLibrary"] = _connectionString
                    });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<BoardLibraryDbContext>>();
                services.RemoveAll<BoardLibraryDbContext>();
                services.AddDbContext<BoardLibraryDbContext>(options =>
                    options.UseNpgsql(_connectionString));

                services.AddSingleton(new TestAuthClaimsProvider(_claims));
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            });
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
