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
/// Integration tests for the Wave 3 title persistence model and endpoints.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TitlePersistenceIntegrationTests : IAsyncLifetime
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
    /// Verifies title create, publish, metadata revisioning, public catalog retrieval, and metadata rollback against PostgreSQL.
    /// </summary>
    [Fact]
    public async Task TitleEndpoints_WithRealPostgres_RoundTripPersistedData()
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
            [
                new Claim("sub", "editor-123"),
                new Claim("name", "Editor User")
            ]);
        using var client = factory.CreateClient();

        using var createResponse = await client.PostAsJsonAsync(
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
        var createPayload = await createResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(createPayload);
        var titleId = Guid.Parse(createDocument.RootElement.GetProperty("title").GetProperty("id").GetString()!);

        using var publishResponse = await client.PutAsJsonAsync(
            $"/developer/titles/{titleId}",
            new
            {
                slug = "star-blasters",
                contentKind = "game",
                lifecycleStatus = "testing",
                visibility = "listed"
            });
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);

        using var metadataUpdateResponse = await client.PutAsJsonAsync(
            $"/developer/titles/{titleId}/metadata/current",
            new
            {
                displayName = "Star Blasters",
                shortDescription = "Family space battles in short rounds.",
                description = "Updated testing copy after first public feedback round.",
                genreDisplay = "Arcade Shooter",
                minPlayers = 1,
                maxPlayers = 4,
                ageRatingAuthority = "ESRB",
                ageRatingValue = "E10+",
                minAgeYears = 10
            });
        var metadataUpdatePayload = await metadataUpdateResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, metadataUpdateResponse.StatusCode);

        using var metadataUpdateDocument = JsonDocument.Parse(metadataUpdatePayload);
        Assert.Equal(2, metadataUpdateDocument.RootElement.GetProperty("title").GetProperty("currentMetadataRevision").GetInt32());

        using var publicListResponse = await client.GetAsync("/catalog");
        var publicListPayload = await publicListResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, publicListResponse.StatusCode);

        using var publicListDocument = JsonDocument.Parse(publicListPayload);
        Assert.Single(publicListDocument.RootElement.GetProperty("titles").EnumerateArray());
        Assert.Equal(1, publicListDocument.RootElement.GetProperty("paging").GetProperty("totalCount").GetInt32());
        Assert.Equal(1, publicListDocument.RootElement.GetProperty("paging").GetProperty("totalPages").GetInt32());

        using var publicDetailResponse = await client.GetAsync("/catalog/stellar-forge/star-blasters");
        var publicDetailPayload = await publicDetailResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, publicDetailResponse.StatusCode);

        using var publicDetailDocument = JsonDocument.Parse(publicDetailPayload);
        Assert.Equal(
            "Updated testing copy after first public feedback round.",
            publicDetailDocument.RootElement.GetProperty("title").GetProperty("description").GetString());

        using var activateResponse = await client.PostAsync($"/developer/titles/{titleId}/metadata-versions/1/activate", content: null);
        var activatePayload = await activateResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

        using var activateDocument = JsonDocument.Parse(activatePayload);
        Assert.Equal(1, activateDocument.RootElement.GetProperty("title").GetProperty("currentMetadataRevision").GetInt32());

        await using var verificationContext = CreateDbContext();
        var title = await verificationContext.Titles.SingleAsync(candidate => candidate.Id == titleId);
        var metadataVersions = await verificationContext.TitleMetadataVersions
            .Where(candidate => candidate.TitleId == titleId)
            .OrderBy(candidate => candidate.RevisionNumber)
            .ToListAsync();

        Assert.Equal(2, metadataVersions.Count);
        Assert.Equal(metadataVersions[0].Id, title.CurrentMetadataVersionId);
    }

    /// <summary>
    /// Verifies title slug uniqueness is scoped to the owning organization.
    /// </summary>
    [Fact]
    public async Task Schema_WithScopedTitleSlugs_AllowsReuseAcrossOrganizationsButRejectsDuplicatesWithinOrganization()
    {
        var firstOrganizationId = Guid.NewGuid();
        var secondOrganizationId = Guid.NewGuid();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();

        dbContext.Organizations.AddRange(
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

        dbContext.Titles.AddRange(
            new Title
            {
                Id = Guid.NewGuid(),
                OrganizationId = firstOrganizationId,
                Slug = "star-blasters",
                ContentKind = "game",
                LifecycleStatus = "draft",
                Visibility = "private",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new Title
            {
                Id = Guid.NewGuid(),
                OrganizationId = secondOrganizationId,
                Slug = "star-blasters",
                ContentKind = "game",
                LifecycleStatus = "draft",
                Visibility = "private",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });

        await dbContext.SaveChangesAsync();

        dbContext.Titles.Add(new Title
        {
            Id = Guid.NewGuid(),
            OrganizationId = firstOrganizationId,
            Slug = "star-blasters",
            ContentKind = "game",
            LifecycleStatus = "draft",
            Visibility = "private",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    /// <summary>
    /// Verifies metadata revision numbers are unique within each title.
    /// </summary>
    [Fact]
    public async Task Schema_WithDuplicateMetadataRevisionNumber_RejectsSecondRevision()
    {
        var organizationId = Guid.NewGuid();
        var titleId = Guid.NewGuid();

        await using (var dbContext = CreateDbContext())
        {
            await dbContext.Database.MigrateAsync();

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
                LifecycleStatus = "draft",
                Visibility = "private",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();

            dbContext.TitleMetadataVersions.Add(new TitleMetadataVersion
            {
                Id = Guid.NewGuid(),
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
                IsFrozen = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        await using var verificationContext = CreateDbContext();
        verificationContext.TitleMetadataVersions.Add(new TitleMetadataVersion
        {
            Id = Guid.NewGuid(),
            TitleId = titleId,
            RevisionNumber = 1,
            DisplayName = "Star Blasters Duplicate",
            ShortDescription = "Duplicate revision.",
            Description = "Duplicate revision description.",
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

        await Assert.ThrowsAsync<DbUpdateException>(() => verificationContext.SaveChangesAsync());
    }

    /// <summary>
    /// Verifies a title cannot point its current metadata pointer at another title's metadata revision.
    /// </summary>
    [Fact]
    public async Task Schema_WithCrossTitleCurrentMetadataPointer_RejectsInvalidReference()
    {
        var organizationId = Guid.NewGuid();
        var firstTitleId = Guid.NewGuid();
        var secondTitleId = Guid.NewGuid();
        var foreignMetadataId = Guid.NewGuid();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();

        dbContext.Organizations.Add(new Organization
        {
            Id = organizationId,
            Slug = "stellar-forge",
            DisplayName = "Stellar Forge",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        dbContext.Titles.AddRange(
            new Title
            {
                Id = firstTitleId,
                OrganizationId = organizationId,
                Slug = "star-blasters",
                ContentKind = "game",
                LifecycleStatus = "draft",
                Visibility = "private",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new Title
            {
                Id = secondTitleId,
                OrganizationId = organizationId,
                Slug = "puzzle-grove",
                ContentKind = "game",
                LifecycleStatus = "draft",
                Visibility = "private",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        dbContext.TitleMetadataVersions.Add(new TitleMetadataVersion
        {
            Id = foreignMetadataId,
            TitleId = secondTitleId,
            RevisionNumber = 1,
            DisplayName = "Puzzle Grove",
            ShortDescription = "Foreign metadata.",
            Description = "Metadata owned by another title.",
            GenreDisplay = "Puzzle",
            MinPlayers = 1,
            MaxPlayers = 1,
            AgeRatingAuthority = "PEGI",
            AgeRatingValue = "7",
            MinAgeYears = 7,
            IsFrozen = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var firstTitle = await dbContext.Titles.SingleAsync(candidate => candidate.Id == firstTitleId);
        firstTitle.CurrentMetadataVersionId = foreignMetadataId;

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    /// <summary>
    /// Verifies duplicate title creation returns the public conflict payload against PostgreSQL.
    /// </summary>
    [Fact]
    public async Task CreateTitleEndpoint_WithDuplicateSlug_ReturnsConflict()
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
            [
                new Claim("sub", "editor-123"),
                new Claim("name", "Editor User")
            ]);
        using var client = factory.CreateClient();

        using var firstResponse = await client.PostAsJsonAsync(
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
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var secondResponse = await client.PostAsJsonAsync(
            $"/developer/organizations/{organizationId}/titles",
            new
            {
                slug = "star-blasters",
                contentKind = "game",
                lifecycleStatus = "draft",
                visibility = "private",
                metadata = new
                {
                    displayName = "Duplicate Star Blasters",
                    shortDescription = "Duplicate short description.",
                    description = "Duplicate description.",
                    genreDisplay = "Arcade Shooter",
                    minPlayers = 1,
                    maxPlayers = 4,
                    ageRatingAuthority = "ESRB",
                    ageRatingValue = "E10+",
                    minAgeYears = 10
                }
            });
        var payload = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("title_slug_conflict", document.RootElement.GetProperty("code").GetString());
    }

    /// <summary>
    /// Verifies title updates return the public conflict payload when a duplicate slug is requested.
    /// </summary>
    [Fact]
    public async Task UpdateTitleEndpoint_WithDuplicateSlug_ReturnsConflict()
    {
        await using (var migrationContext = CreateDbContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        var organizationId = Guid.NewGuid();
        var editorUserId = Guid.NewGuid();
        var firstTitleId = Guid.NewGuid();
        var secondTitleId = Guid.NewGuid();
        var firstMetadataId = Guid.NewGuid();
        var secondMetadataId = Guid.NewGuid();

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
            seedContext.Titles.AddRange(
                new Title
                {
                    Id = firstTitleId,
                    OrganizationId = organizationId,
                    Slug = "star-blasters",
                    ContentKind = "game",
                    LifecycleStatus = "draft",
                    Visibility = "private",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new Title
                {
                    Id = secondTitleId,
                    OrganizationId = organizationId,
                    Slug = "puzzle-grove",
                    ContentKind = "game",
                    LifecycleStatus = "draft",
                    Visibility = "private",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            await seedContext.SaveChangesAsync();

            seedContext.TitleMetadataVersions.AddRange(
                new TitleMetadataVersion
                {
                    Id = firstMetadataId,
                    TitleId = firstTitleId,
                    RevisionNumber = 1,
                    DisplayName = "Star Blasters",
                    ShortDescription = "First short description.",
                    Description = "First description.",
                    GenreDisplay = "Arcade Shooter",
                    MinPlayers = 1,
                    MaxPlayers = 4,
                    AgeRatingAuthority = "ESRB",
                    AgeRatingValue = "E10+",
                    MinAgeYears = 10,
                    IsFrozen = false,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new TitleMetadataVersion
                {
                    Id = secondMetadataId,
                    TitleId = secondTitleId,
                    RevisionNumber = 1,
                    DisplayName = "Puzzle Grove",
                    ShortDescription = "Second short description.",
                    Description = "Second description.",
                    GenreDisplay = "Puzzle",
                    MinPlayers = 1,
                    MaxPlayers = 1,
                    AgeRatingAuthority = "PEGI",
                    AgeRatingValue = "3",
                    MinAgeYears = 3,
                    IsFrozen = false,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            await seedContext.SaveChangesAsync();

            var titles = await seedContext.Titles
                .Where(candidate => candidate.Id == firstTitleId || candidate.Id == secondTitleId)
                .ToListAsync();
            titles.Single(candidate => candidate.Id == firstTitleId).CurrentMetadataVersionId = firstMetadataId;
            titles.Single(candidate => candidate.Id == secondTitleId).CurrentMetadataVersionId = secondMetadataId;
            await seedContext.SaveChangesAsync();
        }

        using var factory = new RealPostgresApiFactory(
            _postgresContainer.GetConnectionString(),
            [
                new Claim("sub", "editor-123"),
                new Claim("name", "Editor User")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            $"/developer/titles/{secondTitleId}",
            new
            {
                slug = "star-blasters",
                contentKind = "game",
                lifecycleStatus = "draft",
                visibility = "private"
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("title_slug_conflict", document.RootElement.GetProperty("code").GetString());
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
