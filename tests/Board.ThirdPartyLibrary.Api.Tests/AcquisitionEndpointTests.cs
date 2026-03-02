using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Board.ThirdPartyLibrary.Api.Persistence;
using Board.ThirdPartyLibrary.Api.Persistence.Configurations;
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

namespace Board.ThirdPartyLibrary.Api.Tests;

/// <summary>
/// Endpoint tests for the Wave 5 supported publisher and acquisition binding API surface.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AcquisitionEndpointTests
{
    [Fact]
    public async Task ListSupportedPublishersEndpoint_ReturnsEnabledPublishers()
    {
        using var factory = new TestApiFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.SupportedPublishers.AddRange(
                new SupportedPublisher
                {
                    Id = SupportedPublisherConfiguration.ItchIoId,
                    Key = "itch-io",
                    DisplayName = "itch.io",
                    HomepageUrl = "https://itch.io/",
                    IsEnabled = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new SupportedPublisher
                {
                    Id = Guid.NewGuid(),
                    Key = "disabled-store",
                    DisplayName = "Disabled Store",
                    HomepageUrl = "https://disabled.example/",
                    IsEnabled = false,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/supported-publishers");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var publishers = document.RootElement.GetProperty("supportedPublishers").EnumerateArray().ToList();
        Assert.Single(publishers);
        Assert.Equal("itch.io", publishers[0].GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task ListCatalogTitlesEndpoint_WithPrimaryBinding_ReturnsAcquisitionUrl()
    {
        using var factory = new TestApiFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            await SeedPublicTitleWithAcquisitionAsync(dbContext);
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/catalog?organizationSlug=stellar-forge&contentKind=game");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var title = document.RootElement.GetProperty("titles")[0];
        Assert.Equal("https://stellar-forge.itch.io/star-blasters", title.GetProperty("acquisitionUrl").GetString());
    }

    [Fact]
    public async Task GetCatalogTitleEndpoint_WithPrimaryBinding_ReturnsAcquisitionDetails()
    {
        using var factory = new TestApiFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            await SeedPublicTitleWithAcquisitionAsync(dbContext);
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/catalog/stellar-forge/star-blasters");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var acquisition = document.RootElement.GetProperty("title").GetProperty("acquisition");
        Assert.Equal("View on itch.io", acquisition.GetProperty("label").GetString());
        Assert.Equal("itch.io", acquisition.GetProperty("providerDisplayName").GetString());
    }

    [Fact]
    public async Task CreateIntegrationConnectionEndpoint_WithEditorMembership_PersistsSupportedPublisherConnection()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "editor-123")]);

        Guid organizationId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            organizationId = await SeedManagedOrganizationAsync(dbContext, "editor-123");
            dbContext.SupportedPublishers.Add(new SupportedPublisher
            {
                Id = SupportedPublisherConfiguration.ItchIoId,
                Key = "itch-io",
                DisplayName = "itch.io",
                HomepageUrl = "https://itch.io/",
                IsEnabled = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"/developer/organizations/{organizationId}/integration-connections",
            new
            {
                supportedPublisherId = SupportedPublisherConfiguration.ItchIoId,
                configuration = new
                {
                    widgetTheme = "dark"
                },
                isEnabled = true
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var connection = await verificationDbContext.IntegrationConnections.SingleAsync(candidate => candidate.OrganizationId == organizationId);
        Assert.Equal(SupportedPublisherConfiguration.ItchIoId, connection.SupportedPublisherId);
        Assert.Equal("{\"widgetTheme\":\"dark\"}", connection.ConfigurationJson);
    }

    [Fact]
    public async Task CreateIntegrationConnectionEndpoint_WithInvalidPayload_ReturnsUnprocessableEntity()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "editor-123")]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.Add(new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "editor-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"/developer/organizations/{Guid.NewGuid()}/integration-connections",
            new
            {
                customPublisherDisplayName = "Custom Store",
                isEnabled = true
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var errors = document.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("customPublisherHomepageUrl", out _));
    }

    [Fact]
    public async Task DeleteIntegrationConnectionEndpoint_WhenBindingExists_ReturnsConflict()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "editor-123")]);

        Guid organizationId;
        Guid connectionId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            (_, organizationId, connectionId, _) = await SeedManagedTitleWithBindingAsync(dbContext, "editor-123");
        }

        using var client = factory.CreateClient();
        using var response = await client.DeleteAsync($"/developer/organizations/{organizationId}/integration-connections/{connectionId}");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("integration_connection_in_use", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateTitleIntegrationBindingEndpoint_WithCrossOrganizationConnection_ReturnsConflict()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "editor-123")]);

        Guid titleId;
        Guid foreignConnectionId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            titleId = await SeedManagedTitleWithMetadataAsync(dbContext, "editor-123");
            foreignConnectionId = await SeedForeignOrganizationConnectionAsync(dbContext);
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"/developer/titles/{titleId}/integration-bindings",
            new
            {
                integrationConnectionId = foreignConnectionId,
                acquisitionUrl = "https://foreign.example/star-blasters",
                acquisitionLabel = "Foreign Store",
                isPrimary = true,
                isEnabled = true
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("title_integration_organization_conflict", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateTitleIntegrationBindingEndpoint_WithInvalidPayload_ReturnsUnprocessableEntity()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "editor-123")]);

        Guid titleId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            titleId = await SeedManagedTitleWithMetadataAsync(dbContext, "editor-123");
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"/developer/titles/{titleId}/integration-bindings",
            new
            {
                integrationConnectionId = Guid.Empty,
                acquisitionUrl = "http://not-https.example/game",
                isPrimary = true,
                isEnabled = false
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var errors = document.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("integrationConnectionId", out _));
        Assert.True(errors.TryGetProperty("acquisitionUrl", out _));
        Assert.True(errors.TryGetProperty("isPrimary", out _));
    }

    [Fact]
    public async Task DeleteTitleIntegrationBindingEndpoint_WhenDeletingPrimaryWithOtherEnabledBindings_ReturnsConflict()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "editor-123")]);

        Guid titleId;
        Guid primaryBindingId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            (titleId, _, _, primaryBindingId) = await SeedManagedTitleWithBindingAsync(dbContext, "editor-123", includeSecondaryEnabledBinding: true);
        }

        using var client = factory.CreateClient();
        using var response = await client.DeleteAsync($"/developer/titles/{titleId}/integration-bindings/{primaryBindingId}");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("title_integration_primary_required", document.RootElement.GetProperty("code").GetString());
    }

    private static async Task SeedPublicTitleWithAcquisitionAsync(BoardLibraryDbContext dbContext)
    {
        var organizationId = Guid.NewGuid();
        var titleId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var bindingId = Guid.NewGuid();

        dbContext.Organizations.Add(new Organization
        {
            Id = organizationId,
            Slug = "stellar-forge",
            DisplayName = "Stellar Forge",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        dbContext.SupportedPublishers.Add(new SupportedPublisher
        {
            Id = SupportedPublisherConfiguration.ItchIoId,
            Key = "itch-io",
            DisplayName = "itch.io",
            HomepageUrl = "https://itch.io/",
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        dbContext.Titles.Add(new Title
        {
            Id = titleId,
            OrganizationId = organizationId,
            Slug = "star-blasters",
            ContentKind = "game",
            LifecycleStatus = "testing",
            Visibility = "listed",
            CurrentMetadataVersionId = metadataId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        dbContext.TitleMetadataVersions.Add(new TitleMetadataVersion
        {
            Id = metadataId,
            TitleId = titleId,
            RevisionNumber = 2,
            DisplayName = "Star Blasters",
            ShortDescription = "Family space battles in short rounds.",
            Description = "Pilot colorful starships through family-friendly arena battles built for the Board console.",
            GenreDisplay = "Arcade Shooter",
            MinPlayers = 1,
            MaxPlayers = 4,
            AgeRatingAuthority = "ESRB",
            AgeRatingValue = "E10+",
            MinAgeYears = 10,
            IsFrozen = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        dbContext.IntegrationConnections.Add(new IntegrationConnection
        {
            Id = connectionId,
            OrganizationId = organizationId,
            SupportedPublisherId = SupportedPublisherConfiguration.ItchIoId,
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        dbContext.TitleIntegrationBindings.Add(new TitleIntegrationBinding
        {
            Id = bindingId,
            TitleId = titleId,
            IntegrationConnectionId = connectionId,
            AcquisitionUrl = "https://stellar-forge.itch.io/star-blasters",
            AcquisitionLabel = "View on itch.io",
            IsPrimary = true,
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid> SeedManagedOrganizationAsync(BoardLibraryDbContext dbContext, string subject)
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new AppUser
        {
            Id = userId,
            KeycloakSubject = subject,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        dbContext.Organizations.Add(new Organization
        {
            Id = organizationId,
            Slug = "stellar-forge",
            DisplayName = "Stellar Forge",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        dbContext.OrganizationMemberships.Add(new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = "editor",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        return organizationId;
    }

    private static async Task<Guid> SeedManagedTitleWithMetadataAsync(BoardLibraryDbContext dbContext, string subject)
    {
        var organizationId = await SeedManagedOrganizationAsync(dbContext, subject);
        var titleId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();
        dbContext.Titles.Add(new Title
        {
            Id = titleId,
            OrganizationId = organizationId,
            Slug = "star-blasters",
            ContentKind = "game",
            LifecycleStatus = "draft",
            Visibility = "private",
            CurrentMetadataVersionId = metadataId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        dbContext.TitleMetadataVersions.Add(new TitleMetadataVersion
        {
            Id = metadataId,
            TitleId = titleId,
            RevisionNumber = 1,
            DisplayName = "Star Blasters",
            ShortDescription = "Draft short description.",
            Description = "Draft description.",
            GenreDisplay = "Arcade Shooter",
            MinPlayers = 1,
            MaxPlayers = 4,
            AgeRatingAuthority = "ESRB",
            AgeRatingValue = "E10+",
            MinAgeYears = 10,
            IsFrozen = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        return titleId;
    }

    private static async Task<(Guid TitleId, Guid OrganizationId, Guid ConnectionId, Guid PrimaryBindingId)> SeedManagedTitleWithBindingAsync(
        BoardLibraryDbContext dbContext,
        string subject,
        bool includeSecondaryEnabledBinding = false)
    {
        var titleId = await SeedManagedTitleWithMetadataAsync(dbContext, subject);
        var title = await dbContext.Titles.SingleAsync(candidate => candidate.Id == titleId);
        var connectionId = Guid.NewGuid();
        var secondaryConnectionId = Guid.NewGuid();
        var primaryBindingId = Guid.NewGuid();

        dbContext.SupportedPublishers.Add(new SupportedPublisher
        {
            Id = SupportedPublisherConfiguration.ItchIoId,
            Key = "itch-io",
            DisplayName = "itch.io",
            HomepageUrl = "https://itch.io/",
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        dbContext.IntegrationConnections.AddRange(
            new IntegrationConnection
            {
                Id = connectionId,
                OrganizationId = title.OrganizationId,
                SupportedPublisherId = SupportedPublisherConfiguration.ItchIoId,
                IsEnabled = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new IntegrationConnection
            {
                Id = secondaryConnectionId,
                OrganizationId = title.OrganizationId,
                CustomPublisherDisplayName = "Stellar Forge Direct",
                CustomPublisherHomepageUrl = "https://store.stellar-forge.example/",
                IsEnabled = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        dbContext.TitleIntegrationBindings.Add(new TitleIntegrationBinding
        {
            Id = primaryBindingId,
            TitleId = titleId,
            IntegrationConnectionId = connectionId,
            AcquisitionUrl = "https://stellar-forge.itch.io/star-blasters",
            AcquisitionLabel = "View on itch.io",
            IsPrimary = true,
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        if (includeSecondaryEnabledBinding)
        {
            dbContext.TitleIntegrationBindings.Add(new TitleIntegrationBinding
            {
                Id = Guid.NewGuid(),
                TitleId = titleId,
                IntegrationConnectionId = secondaryConnectionId,
                AcquisitionUrl = "https://store.stellar-forge.example/star-blasters",
                AcquisitionLabel = "Developer direct store",
                IsPrimary = false,
                IsEnabled = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();
        return (titleId, title.OrganizationId, connectionId, primaryBindingId);
    }

    private static async Task<Guid> SeedForeignOrganizationConnectionAsync(BoardLibraryDbContext dbContext)
    {
        var organizationId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        dbContext.Organizations.Add(new Organization
        {
            Id = organizationId,
            Slug = "foreign-org",
            DisplayName = "Foreign Org",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        dbContext.IntegrationConnections.Add(new IntegrationConnection
        {
            Id = connectionId,
            OrganizationId = organizationId,
            CustomPublisherDisplayName = "Foreign Store",
            CustomPublisherHomepageUrl = "https://foreign.example/",
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        return connectionId;
    }

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        private readonly bool _useTestAuthentication;
        private readonly IReadOnlyList<Claim> _testClaims;
        private readonly string _inMemoryDatabaseName = $"board-third-party-lib-acquisition-tests-{Guid.NewGuid():N}";

        public TestApiFactory(bool useTestAuthentication = false, IEnumerable<Claim>? testClaims = null)
        {
            _useTestAuthentication = useTestAuthentication;
            _testClaims = testClaims?.ToList() ?? [];
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
            });

            builder.ConfigureTestServices(services =>
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
            });
        }
    }

    private sealed class TestAuthClaimsProvider(IReadOnlyList<Claim> claims)
    {
        public IReadOnlyList<Claim> Claims { get; } = claims;
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TestAuthClaimsProvider claimsProvider)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(claimsProvider.Claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
