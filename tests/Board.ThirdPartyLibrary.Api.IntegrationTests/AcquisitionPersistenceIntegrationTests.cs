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
using Testcontainers.PostgreSql;

namespace Board.ThirdPartyLibrary.Api.IntegrationTests;

/// <summary>
/// Integration tests for the Wave 5 publisher and acquisition persistence model and endpoints.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AcquisitionPersistenceIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("board_tpl")
        .WithUsername("board_tpl_user")
        .WithPassword("board_tpl_password")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
    }

    [Fact]
    public async Task Wave5Endpoints_WithRealPostgres_RoundTripPersistedData()
    {
        await using (var migrationContext = CreateDbContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        var organizationId = Guid.NewGuid();
        var editorUserId = Guid.NewGuid();

        await using (var seedContext = CreateDbContext())
        {
            seedContext.Users.Add(new AppUser
            {
                Id = editorUserId,
                KeycloakSubject = "editor-123",
                DisplayName = "Editor User",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            seedContext.Organizations.Add(new Organization
            {
                Id = organizationId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            seedContext.OrganizationMemberships.Add(new OrganizationMembership
            {
                OrganizationId = organizationId,
                UserId = editorUserId,
                Role = "editor",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await seedContext.SaveChangesAsync();
        }

        using var factory = new RealPostgresApiFactory(
            _postgresContainer.GetConnectionString(),
            [new Claim("sub", "editor-123"), new Claim("name", "Editor User")]);
        using var client = factory.CreateClient();

        using var createTitleResponse = await client.PostAsJsonAsync(
            $"/developer/organizations/{organizationId}/titles",
            new
            {
                slug = "star-blasters",
                contentKind = "game",
                lifecycleStatus = "testing",
                visibility = "listed",
                metadata = new
                {
                    displayName = "Star Blasters",
                    shortDescription = "Family space battles in short rounds.",
                    description = "Pilot colorful starships through family-friendly arena battles built for the Board console.",
                    genreDisplay = "Arcade Shooter",
                    minPlayers = 1,
                    maxPlayers = 4,
                    ageRatingAuthority = "ESRB",
                    ageRatingValue = "E10+",
                    minAgeYears = 10
                }
            });
        var createTitlePayload = await createTitleResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, createTitleResponse.StatusCode);

        using var createTitleDocument = JsonDocument.Parse(createTitlePayload);
        var titleId = Guid.Parse(createTitleDocument.RootElement.GetProperty("title").GetProperty("id").GetString()!);

        using var listSupportedPublishersResponse = await client.GetAsync("/supported-publishers");
        var publishersPayload = await listSupportedPublishersResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, listSupportedPublishersResponse.StatusCode);

        using var publishersDocument = JsonDocument.Parse(publishersPayload);
        var itchPublisherId = Guid.Parse(
            publishersDocument.RootElement.GetProperty("supportedPublishers")
                .EnumerateArray()
                .Single(candidate => candidate.GetProperty("key").GetString() == "itch-io")
                .GetProperty("id")
                .GetString()!);

        using var createConnectionResponse = await client.PostAsJsonAsync(
            $"/developer/organizations/{organizationId}/integration-connections",
            new
            {
                supportedPublisherId = itchPublisherId,
                configuration = new
                {
                    widgetTheme = "dark"
                },
                isEnabled = true
            });
        var createConnectionPayload = await createConnectionResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, createConnectionResponse.StatusCode);

        using var createConnectionDocument = JsonDocument.Parse(createConnectionPayload);
        var connectionId = Guid.Parse(createConnectionDocument.RootElement.GetProperty("integrationConnection").GetProperty("id").GetString()!);

        using var createBindingResponse = await client.PostAsJsonAsync(
            $"/developer/titles/{titleId}/integration-bindings",
            new
            {
                integrationConnectionId = connectionId,
                acquisitionUrl = "https://stellar-forge.itch.io/star-blasters",
                acquisitionLabel = "View on itch.io",
                configuration = new
                {
                    embedWidget = true
                },
                isPrimary = true,
                isEnabled = true
            });
        Assert.Equal(HttpStatusCode.Created, createBindingResponse.StatusCode);

        using var publicListResponse = await client.GetAsync("/catalog?organizationSlug=stellar-forge&contentKind=game");
        var publicListPayload = await publicListResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, publicListResponse.StatusCode);

        using var publicListDocument = JsonDocument.Parse(publicListPayload);
        Assert.Equal(
            "https://stellar-forge.itch.io/star-blasters",
            publicListDocument.RootElement.GetProperty("titles")[0].GetProperty("acquisitionUrl").GetString());

        using var publicDetailResponse = await client.GetAsync("/catalog/stellar-forge/star-blasters");
        var publicDetailPayload = await publicDetailResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, publicDetailResponse.StatusCode);

        using var publicDetailDocument = JsonDocument.Parse(publicDetailPayload);
        Assert.Equal(
            "itch.io",
            publicDetailDocument.RootElement.GetProperty("title").GetProperty("acquisition").GetProperty("providerDisplayName").GetString());

        using var deleteConnectionResponse = await client.DeleteAsync($"/developer/organizations/{organizationId}/integration-connections/{connectionId}");
        Assert.Equal(HttpStatusCode.Conflict, deleteConnectionResponse.StatusCode);
    }

    [Fact]
    public async Task Schema_WithSecondEnabledPrimaryBinding_RejectsDuplicatePrimary()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();

        var organizationId = Guid.NewGuid();
        var titleId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();
        var firstConnectionId = Guid.NewGuid();
        var secondConnectionId = Guid.NewGuid();

        await SeedTitleWithConnectionsAsync(dbContext, organizationId, titleId, metadataId, firstConnectionId, secondConnectionId);

        dbContext.TitleIntegrationBindings.Add(new TitleIntegrationBinding
        {
            Id = Guid.NewGuid(),
            TitleId = titleId,
            IntegrationConnectionId = firstConnectionId,
            AcquisitionUrl = "https://store-one.example/game",
            IsPrimary = true,
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        dbContext.TitleIntegrationBindings.Add(new TitleIntegrationBinding
        {
            Id = Guid.NewGuid(),
            TitleId = titleId,
            IntegrationConnectionId = secondConnectionId,
            AcquisitionUrl = "https://store-two.example/game",
            IsPrimary = true,
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Schema_WithPrimaryDisabledBinding_RejectsInvalidState()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();

        var organizationId = Guid.NewGuid();
        var titleId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        await SeedTitleWithConnectionsAsync(dbContext, organizationId, titleId, metadataId, connectionId, Guid.NewGuid());

        dbContext.TitleIntegrationBindings.Add(new TitleIntegrationBinding
        {
            Id = Guid.NewGuid(),
            TitleId = titleId,
            IntegrationConnectionId = connectionId,
            AcquisitionUrl = "https://store.example/game",
            IsPrimary = true,
            IsEnabled = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Schema_WithSupportedAndCustomPublisherChoice_RejectsInvalidConnection()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();

        var organizationId = Guid.NewGuid();

        dbContext.Organizations.Add(new Organization
        {
            Id = organizationId,
            Slug = "stellar-forge",
            DisplayName = "Stellar Forge",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        dbContext.IntegrationConnections.Add(new IntegrationConnection
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            SupportedPublisherId = SupportedPublisherConfiguration.ItchIoId,
            CustomPublisherDisplayName = "Should Fail",
            CustomPublisherHomepageUrl = "https://invalid.example/",
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task CreateIntegrationConnectionEndpoint_WithCustomPublisher_RoundTripsPersistedData()
    {
        await using (var migrationContext = CreateDbContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        var organizationId = Guid.NewGuid();
        var editorUserId = Guid.NewGuid();

        await using (var seedContext = CreateDbContext())
        {
            seedContext.Users.Add(new AppUser
            {
                Id = editorUserId,
                KeycloakSubject = "editor-123",
                DisplayName = "Editor User",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            seedContext.Organizations.Add(new Organization
            {
                Id = organizationId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            seedContext.OrganizationMemberships.Add(new OrganizationMembership
            {
                OrganizationId = organizationId,
                UserId = editorUserId,
                Role = "editor",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await seedContext.SaveChangesAsync();
        }

        using var factory = new RealPostgresApiFactory(
            _postgresContainer.GetConnectionString(),
            [new Claim("sub", "editor-123"), new Claim("name", "Editor User")]);
        using var client = factory.CreateClient();

        using var createConnectionResponse = await client.PostAsJsonAsync(
            $"/developer/organizations/{organizationId}/integration-connections",
            new
            {
                customPublisherDisplayName = "Stellar Forge Direct",
                customPublisherHomepageUrl = "https://store.stellar-forge.example/",
                configuration = new
                {
                    checkoutMode = "external"
                },
                isEnabled = true
            });
        var createConnectionPayload = await createConnectionResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, createConnectionResponse.StatusCode);

        using var createConnectionDocument = JsonDocument.Parse(createConnectionPayload);
        var connection = createConnectionDocument.RootElement.GetProperty("integrationConnection");
        Assert.Equal("Stellar Forge Direct", connection.GetProperty("customPublisherDisplayName").GetString());

        using var listConnectionsResponse = await client.GetAsync($"/developer/organizations/{organizationId}/integration-connections");
        var listConnectionsPayload = await listConnectionsResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, listConnectionsResponse.StatusCode);

        using var listConnectionsDocument = JsonDocument.Parse(listConnectionsPayload);
        Assert.Equal(
            "https://store.stellar-forge.example/",
            listConnectionsDocument.RootElement.GetProperty("integrationConnections")[0].GetProperty("customPublisherHomepageUrl").GetString());
    }

    [Fact]
    public async Task CreateTitleIntegrationBindingEndpoint_WithDisabledConnection_ReturnsConflict()
    {
        await using (var migrationContext = CreateDbContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        var organizationId = Guid.NewGuid();
        var editorUserId = Guid.NewGuid();
        var titleId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        await using (var seedContext = CreateDbContext())
        {
            seedContext.Users.Add(new AppUser
            {
                Id = editorUserId,
                KeycloakSubject = "editor-123",
                DisplayName = "Editor User",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            seedContext.Organizations.Add(new Organization
            {
                Id = organizationId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            seedContext.OrganizationMemberships.Add(new OrganizationMembership
            {
                OrganizationId = organizationId,
                UserId = editorUserId,
                Role = "editor",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            seedContext.Titles.Add(new Title
            {
                Id = titleId,
                OrganizationId = organizationId,
                Slug = "star-blasters",
                ContentKind = "game",
                LifecycleStatus = "testing",
                Visibility = "listed",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            seedContext.TitleMetadataVersions.Add(new TitleMetadataVersion
            {
                Id = metadataId,
                TitleId = titleId,
                RevisionNumber = 1,
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
            seedContext.IntegrationConnections.Add(new IntegrationConnection
            {
                Id = connectionId,
                OrganizationId = organizationId,
                CustomPublisherDisplayName = "Disabled Store",
                CustomPublisherHomepageUrl = "https://disabled.example/",
                IsEnabled = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await seedContext.SaveChangesAsync();

            var title = await seedContext.Titles.SingleAsync(candidate => candidate.Id == titleId);
            title.CurrentMetadataVersionId = metadataId;
            await seedContext.SaveChangesAsync();
        }

        using var factory = new RealPostgresApiFactory(
            _postgresContainer.GetConnectionString(),
            [new Claim("sub", "editor-123"), new Claim("name", "Editor User")]);
        using var client = factory.CreateClient();

        using var createBindingResponse = await client.PostAsJsonAsync(
            $"/developer/titles/{titleId}/integration-bindings",
            new
            {
                integrationConnectionId = connectionId,
                acquisitionUrl = "https://disabled.example/star-blasters",
                acquisitionLabel = "Disabled Store",
                isPrimary = true,
                isEnabled = true
            });
        var createBindingPayload = await createBindingResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, createBindingResponse.StatusCode);

        using var createBindingDocument = JsonDocument.Parse(createBindingPayload);
        Assert.Equal(
            "title_integration_connection_disabled",
            createBindingDocument.RootElement.GetProperty("code").GetString());
    }

    private async Task SeedTitleWithConnectionsAsync(
        BoardLibraryDbContext dbContext,
        Guid organizationId,
        Guid titleId,
        Guid metadataId,
        Guid firstConnectionId,
        Guid secondConnectionId)
    {
        dbContext.Organizations.Add(new Organization
        {
            Id = organizationId,
            Slug = "stellar-forge",
            DisplayName = "Stellar Forge",
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
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        dbContext.TitleMetadataVersions.Add(new TitleMetadataVersion
        {
            Id = metadataId,
            TitleId = titleId,
            RevisionNumber = 1,
            DisplayName = "Star Blasters",
            ShortDescription = "Short description.",
            Description = "Description.",
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

        var title = await dbContext.Titles.SingleAsync(candidate => candidate.Id == titleId);
        title.CurrentMetadataVersionId = metadataId;
        dbContext.IntegrationConnections.AddRange(
            new IntegrationConnection
            {
                Id = firstConnectionId,
                OrganizationId = title.OrganizationId,
                SupportedPublisherId = SupportedPublisherConfiguration.ItchIoId,
                IsEnabled = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new IntegrationConnection
            {
                Id = secondConnectionId,
                OrganizationId = title.OrganizationId,
                CustomPublisherDisplayName = "Second Store",
                CustomPublisherHomepageUrl = "https://store-two.example/",
                IsEnabled = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();
    }

    private BoardLibraryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BoardLibraryDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        return new BoardLibraryDbContext(options);
    }

    private sealed class RealPostgresApiFactory(string connectionString, IReadOnlyList<Claim> claims) : WebApplicationFactory<Program>
    {
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
                        ["ConnectionStrings:BoardLibrary"] = connectionString
                    });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<BoardLibraryDbContext>>();
                services.RemoveAll<BoardLibraryDbContext>();
                services.AddDbContext<BoardLibraryDbContext>(options =>
                    options.UseNpgsql(connectionString));

                services.AddSingleton(new TestAuthClaimsProvider(claims));
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
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
