using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
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
/// Endpoint tests for the Wave 3 title and metadata API surface.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TitleEndpointTests
{
    /// <summary>
    /// Verifies the public catalog only lists listed testing or published titles.
    /// </summary>
    [Fact]
    public async Task ListCatalogTitlesEndpoint_ReturnsOnlyPubliclyListedTitles()
    {
        var organizationId = Guid.NewGuid();
        var listedTitleId = Guid.NewGuid();
        var privateTitleId = Guid.NewGuid();

        using var factory = new TestApiFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            var organization = new Organization
            {
                Id = organizationId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            var listedTitle = new Title
            {
                Id = listedTitleId,
                OrganizationId = organizationId,
                Slug = "star-blasters",
                ContentKind = "game",
                LifecycleStatus = "testing",
                Visibility = "listed",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var listedMetadata = new TitleMetadataVersion
            {
                Id = Guid.NewGuid(),
                TitleId = listedTitleId,
                RevisionNumber = 1,
                DisplayName = "Star Blasters",
                ShortDescription = "Family space battles in short rounds.",
                Description = "Pilot colorful starships in family-friendly arena battles.",
                GenreDisplay = "Arcade Shooter",
                MinPlayers = 1,
                MaxPlayers = 4,
                AgeRatingAuthority = "ESRB",
                AgeRatingValue = "E10+",
                MinAgeYears = 10,
                IsFrozen = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            listedTitle.CurrentMetadataVersionId = listedMetadata.Id;

            var privateTitle = new Title
            {
                Id = privateTitleId,
                OrganizationId = organizationId,
                Slug = "secret-prototype",
                ContentKind = "game",
                LifecycleStatus = "testing",
                Visibility = "private",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var privateMetadata = new TitleMetadataVersion
            {
                Id = Guid.NewGuid(),
                TitleId = privateTitleId,
                RevisionNumber = 1,
                DisplayName = "Secret Prototype",
                ShortDescription = "Not publicly visible.",
                Description = "Private test content.",
                GenreDisplay = "Action",
                MinPlayers = 1,
                MaxPlayers = 1,
                AgeRatingAuthority = "ESRB",
                AgeRatingValue = "E",
                MinAgeYears = 6,
                IsFrozen = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            privateTitle.CurrentMetadataVersionId = privateMetadata.Id;

            dbContext.Organizations.Add(organization);
            dbContext.Titles.AddRange(listedTitle, privateTitle);
            dbContext.TitleMetadataVersions.AddRange(listedMetadata, privateMetadata);
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/catalog");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var titles = document.RootElement.GetProperty("titles").EnumerateArray().ToList();

        Assert.Single(titles);
        Assert.Equal("star-blasters", titles[0].GetProperty("slug").GetString());
    }

    /// <summary>
    /// Verifies unlisted testing titles are retrievable by direct catalog route.
    /// </summary>
    [Fact]
    public async Task GetCatalogTitleEndpoint_WithUnlistedTestingTitle_ReturnsDetail()
    {
        var organizationId = Guid.NewGuid();
        var titleId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();

        using var factory = new TestApiFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
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
                Visibility = "unlisted",
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
                Description = "Pilot colorful starships in family-friendly arena battles.",
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

            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/catalog/stellar-forge/star-blasters");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var title = document.RootElement.GetProperty("title");

        Assert.Equal("unlisted", title.GetProperty("visibility").GetString());
        Assert.Equal(2, title.GetProperty("currentMetadataRevision").GetInt32());
    }

    /// <summary>
    /// Verifies editors can create titles with required initial metadata.
    /// </summary>
    [Fact]
    public async Task CreateTitleEndpoint_WithEditorMembership_PersistsTitleAndMetadata()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "editor-123"),
                new Claim("name", "Editor User")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.Add(new AppUser
            {
                Id = userId,
                KeycloakSubject = "editor-123",
                DisplayName = "Editor User",
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
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"/developer/organizations/{organizationId}/titles",
            new
            {
                slug = "star-blasters",
                contentKind = "game",
                lifecycleStatus = "draft",
                visibility = "private",
                metadata = new
                {
                    displayName = "Star Blasters",
                    shortDescription = "Family space battles in short rounds.",
                    description = "Pilot colorful starships in family-friendly arena battles.",
                    genreDisplay = "Arcade Shooter",
                    minPlayers = 1,
                    maxPlayers = 4,
                    ageRatingAuthority = "ESRB",
                    ageRatingValue = "E10+",
                    minAgeYears = 10
                }
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var titleId = Guid.Parse(document.RootElement.GetProperty("title").GetProperty("id").GetString()!);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var title = await verificationDbContext.Titles.SingleAsync(candidate => candidate.Id == titleId);
        var metadata = await verificationDbContext.TitleMetadataVersions.SingleAsync(candidate => candidate.TitleId == titleId);

        Assert.Equal("star-blasters", title.Slug);
        Assert.Equal(1, metadata.RevisionNumber);
        Assert.False(metadata.IsFrozen);
    }

    /// <summary>
    /// Verifies invalid title payloads are rejected.
    /// </summary>
    [Fact]
    public async Task CreateTitleEndpoint_WithInvalidPayload_ReturnsUnprocessableEntity()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "editor-123")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.Add(new AppUser
            {
                Id = userId,
                KeycloakSubject = "editor-123",
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
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"/developer/organizations/{organizationId}/titles",
            new
            {
                slug = "Invalid Slug",
                contentKind = "invalid",
                lifecycleStatus = "draft",
                visibility = "listed",
                metadata = new
                {
                    displayName = "",
                    shortDescription = "",
                    description = "",
                    genreDisplay = "",
                    minPlayers = 0,
                    maxPlayers = -1,
                    ageRatingAuthority = "",
                    ageRatingValue = "",
                    minAgeYears = -2
                }
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode)StatusCodes.Status422UnprocessableEntity, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var errors = document.RootElement.GetProperty("errors");

        Assert.True(errors.TryGetProperty("slug", out _));
        Assert.True(errors.TryGetProperty("contentKind", out _));
        Assert.True(errors.TryGetProperty("visibility", out _));
        Assert.True(errors.TryGetProperty("metadata.displayName", out _));
        Assert.True(errors.TryGetProperty("metadata.minPlayers", out _));
    }

    /// <summary>
    /// Verifies draft metadata updates happen in place until the title leaves draft.
    /// </summary>
    [Fact]
    public async Task UpdateCurrentMetadataEndpoint_ForDraftTitle_UpdatesExistingRevision()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var titleId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();

        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "editor-123")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.Add(new AppUser
            {
                Id = userId,
                KeycloakSubject = "editor-123",
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
                ShortDescription = "Original short description.",
                Description = "Original description.",
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
        }

        using var client = factory.CreateClient();
        using var response = await client.PutAsJsonAsync(
            $"/developer/titles/{titleId}/metadata/current",
            new
            {
                displayName = "Star Blasters",
                shortDescription = "Updated short description.",
                description = "Updated description.",
                genreDisplay = "Arcade Shooter",
                minPlayers = 1,
                maxPlayers = 4,
                ageRatingAuthority = "ESRB",
                ageRatingValue = "E10+",
                minAgeYears = 10
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var metadataVersions = await verificationDbContext.TitleMetadataVersions
            .Where(candidate => candidate.TitleId == titleId)
            .ToListAsync();

        Assert.Single(metadataVersions);
        Assert.Equal("Updated short description.", metadataVersions[0].ShortDescription);
    }

    /// <summary>
    /// Verifies public metadata updates after draft create a new revision.
    /// </summary>
    [Fact]
    public async Task UpdateCurrentMetadataEndpoint_ForTestingTitle_CreatesNewRevision()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var titleId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();

        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "editor-123")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.Add(new AppUser
            {
                Id = userId,
                KeycloakSubject = "editor-123",
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
                RevisionNumber = 1,
                DisplayName = "Star Blasters",
                ShortDescription = "Original short description.",
                Description = "Original description.",
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
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.PutAsJsonAsync(
            $"/developer/titles/{titleId}/metadata/current",
            new
            {
                displayName = "Star Blasters",
                shortDescription = "Updated short description.",
                description = "Updated description.",
                genreDisplay = "Arcade Shooter",
                minPlayers = 1,
                maxPlayers = 4,
                ageRatingAuthority = "ESRB",
                ageRatingValue = "E10+",
                minAgeYears = 10
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal(2, document.RootElement.GetProperty("title").GetProperty("currentMetadataRevision").GetInt32());

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var metadataVersions = await verificationDbContext.TitleMetadataVersions
            .Where(candidate => candidate.TitleId == titleId)
            .OrderBy(candidate => candidate.RevisionNumber)
            .ToListAsync();

        Assert.Equal(2, metadataVersions.Count);
        Assert.True(metadataVersions.All(candidate => candidate.IsFrozen));
    }

    /// <summary>
    /// Verifies activating an older metadata revision repoints the current metadata pointer.
    /// </summary>
    [Fact]
    public async Task ActivateMetadataVersionEndpoint_WithExistingRevision_SwitchesCurrentRevision()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var titleId = Guid.NewGuid();
        var revisionOneId = Guid.NewGuid();
        var revisionTwoId = Guid.NewGuid();

        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "editor-123")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.Add(new AppUser
            {
                Id = userId,
                KeycloakSubject = "editor-123",
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
            dbContext.Titles.Add(new Title
            {
                Id = titleId,
                OrganizationId = organizationId,
                Slug = "star-blasters",
                ContentKind = "game",
                LifecycleStatus = "testing",
                Visibility = "listed",
                CurrentMetadataVersionId = revisionTwoId,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.TitleMetadataVersions.AddRange(
                new TitleMetadataVersion
                {
                    Id = revisionOneId,
                    TitleId = titleId,
                    RevisionNumber = 1,
                    DisplayName = "Star Blasters",
                    ShortDescription = "Revision one.",
                    Description = "Revision one description.",
                    GenreDisplay = "Arcade Shooter",
                    MinPlayers = 1,
                    MaxPlayers = 4,
                    AgeRatingAuthority = "ESRB",
                    AgeRatingValue = "E10+",
                    MinAgeYears = 10,
                    IsFrozen = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new TitleMetadataVersion
                {
                    Id = revisionTwoId,
                    TitleId = titleId,
                    RevisionNumber = 2,
                    DisplayName = "Star Blasters",
                    ShortDescription = "Revision two.",
                    Description = "Revision two description.",
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
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsync(
            $"/developer/titles/{titleId}/metadata-versions/1/activate",
            content: null);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal(1, document.RootElement.GetProperty("title").GetProperty("currentMetadataRevision").GetInt32());
    }

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        private readonly Action<IServiceCollection>? _configureServices;
        private readonly Action<IConfigurationBuilder>? _configureConfiguration;
        private readonly bool _useTestAuthentication;
        private readonly IReadOnlyList<Claim> _testClaims;
        private readonly string _inMemoryDatabaseName = $"board-third-party-lib-title-tests-{Guid.NewGuid():N}";

        public TestApiFactory(
            Action<IServiceCollection>? configureServices = null,
            Action<IConfigurationBuilder>? configureConfiguration = null,
            bool useTestAuthentication = false,
            IEnumerable<Claim>? testClaims = null)
        {
            _configureServices = configureServices;
            _configureConfiguration = configureConfiguration;
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

                _configureConfiguration?.Invoke(configurationBuilder);
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

                _configureServices?.Invoke(services);
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
