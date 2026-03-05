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
/// Integration tests for the Wave 4 media, release, and artifact persistence model and endpoints.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TitleWave4PersistenceIntegrationTests : IAsyncLifetime
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
    public async Task Wave4Endpoints_WithRealPostgres_RoundTripPersistedData()
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
                    description = "Pilot colorful starships in family-friendly arena battles.",
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

        using var cardMediaResponse = await client.PutAsJsonAsync(
            $"/developer/titles/{titleId}/media/card",
            new
            {
                sourceUrl = "https://cdn.example.com/card.png",
                altText = "Card art.",
                mimeType = "image/png",
                width = 640,
                height = 360
            });
        Assert.Equal(HttpStatusCode.OK, cardMediaResponse.StatusCode);

        using var heroMediaResponse = await client.PutAsJsonAsync(
            $"/developer/titles/{titleId}/media/hero",
            new
            {
                sourceUrl = "https://cdn.example.com/hero.png",
                altText = "Hero art.",
                mimeType = "image/png",
                width = 1920,
                height = 1080
            });
        Assert.Equal(HttpStatusCode.OK, heroMediaResponse.StatusCode);

        using var createReleaseResponse = await client.PostAsJsonAsync(
            $"/developer/titles/{titleId}/releases",
            new
            {
                version = "1.0.0",
                metadataRevisionNumber = 1
            });
        var createReleasePayload = await createReleaseResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, createReleaseResponse.StatusCode);

        using var createReleaseDocument = JsonDocument.Parse(createReleasePayload);
        var releaseId = Guid.Parse(createReleaseDocument.RootElement.GetProperty("release").GetProperty("id").GetString()!);

        using var createArtifactResponse = await client.PostAsJsonAsync(
            $"/developer/titles/{titleId}/releases/{releaseId}/artifacts",
            new
            {
                artifactKind = "apk",
                packageName = "fun.board.starblasters",
                versionCode = 100L,
                sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                fileSizeBytes = 104857600L
            });
        Assert.Equal(HttpStatusCode.Created, createArtifactResponse.StatusCode);

        using var publishReleaseResponse = await client.PostAsync($"/developer/titles/{titleId}/releases/{releaseId}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publishReleaseResponse.StatusCode);

        using var activateReleaseResponse = await client.PostAsync($"/developer/titles/{titleId}/releases/{releaseId}/activate", null);
        Assert.Equal(HttpStatusCode.OK, activateReleaseResponse.StatusCode);

        using var publicDetailResponse = await client.GetAsync("/catalog/stellar-forge/star-blasters");
        var publicDetailPayload = await publicDetailResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, publicDetailResponse.StatusCode);

        using var publicDetailDocument = JsonDocument.Parse(publicDetailPayload);
        var publicTitle = publicDetailDocument.RootElement.GetProperty("title");
        Assert.Equal("https://cdn.example.com/card.png", publicTitle.GetProperty("cardImageUrl").GetString());
        Assert.Equal(2, publicTitle.GetProperty("mediaAssets").GetArrayLength());
        Assert.Equal("1.0.0", publicTitle.GetProperty("currentRelease").GetProperty("version").GetString());

        using var withdrawReleaseResponse = await client.PostAsync($"/developer/titles/{titleId}/releases/{releaseId}/withdraw", null);
        Assert.Equal(HttpStatusCode.OK, withdrawReleaseResponse.StatusCode);

        await using var verificationContext = CreateDbContext();
        var title = await verificationContext.Titles.SingleAsync(candidate => candidate.Id == titleId);
        var release = await verificationContext.TitleReleases.SingleAsync(candidate => candidate.Id == releaseId);
        var mediaAssets = await verificationContext.TitleMediaAssets.Where(candidate => candidate.TitleId == titleId).ToListAsync();
        var artifacts = await verificationContext.ReleaseArtifacts.Where(candidate => candidate.ReleaseId == releaseId).ToListAsync();

        Assert.Null(title.CurrentReleaseId);
        Assert.Equal("withdrawn", release.Status);
        Assert.Equal(2, mediaAssets.Count);
        Assert.Single(artifacts);
    }

    [Fact]
    public async Task UploadTitleMediaEndpoint_WithUnsupportedFormat_ReturnsValidationError()
    {
        await using (var migrationContext = CreateDbContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        var organizationId = Guid.NewGuid();
        var titleId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();
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
            await SeedTitleWithMetadataAsync(seedContext, organizationId, titleId, metadataId);
        }

        using var factory = new RealPostgresApiFactory(
            _postgresContainer.GetConnectionString(),
            [
                new Claim("sub", "editor-123"),
                new Claim("name", "Editor User")
            ]);
        using var client = factory.CreateClient();
        using var content = new MultipartFormDataContent();
        using var mediaContent = new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        mediaContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(mediaContent, "media", "hero.pdf");

        using var response = await client.PostAsync($"/developer/titles/{titleId}/media/hero/upload", content);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using (var document = JsonDocument.Parse(payload))
        {
            Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("media", out _));
        }

        await using var verificationContext = CreateDbContext();
        Assert.False(await verificationContext.TitleMediaAssets.AnyAsync(candidate => candidate.TitleId == titleId));
    }

    [Fact]
    public async Task Schema_WithDuplicateMediaRole_RejectsSecondRole()
    {
        var organizationId = Guid.NewGuid();
        var titleId = Guid.NewGuid();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
        await SeedTitleAsync(dbContext, organizationId, titleId);

        dbContext.TitleMediaAssets.Add(new TitleMediaAsset
        {
            Id = Guid.NewGuid(),
            TitleId = titleId,
            MediaRole = "card",
            SourceUrl = "https://cdn.example.com/card-one.png",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        dbContext.TitleMediaAssets.Add(new TitleMediaAsset
        {
            Id = Guid.NewGuid(),
            TitleId = titleId,
            MediaRole = "card",
            SourceUrl = "https://cdn.example.com/card-two.png",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Schema_WithDuplicateReleaseVersion_RejectsSecondVersion()
    {
        var organizationId = Guid.NewGuid();
        var titleId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
        await SeedTitleWithMetadataAsync(dbContext, organizationId, titleId, metadataId);

        dbContext.TitleReleases.Add(new TitleRelease
        {
            Id = Guid.NewGuid(),
            TitleId = titleId,
            MetadataVersionId = metadataId,
            Version = "1.0.0",
            Status = "draft",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        dbContext.TitleReleases.Add(new TitleRelease
        {
            Id = Guid.NewGuid(),
            TitleId = titleId,
            MetadataVersionId = metadataId,
            Version = "1.0.0",
            Status = "draft",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Schema_WithCrossTitleCurrentReleasePointer_RejectsInvalidReference()
    {
        var organizationId = Guid.NewGuid();
        var firstTitleId = Guid.NewGuid();
        var secondTitleId = Guid.NewGuid();
        var firstMetadataId = Guid.NewGuid();
        var secondMetadataId = Guid.NewGuid();
        var foreignReleaseId = Guid.NewGuid();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
        await SeedTitleWithMetadataAsync(dbContext, organizationId, firstTitleId, firstMetadataId, "star-blasters");
        await SeedTitleWithMetadataAsync(dbContext, organizationId, secondTitleId, secondMetadataId, "puzzle-grove");

        dbContext.TitleReleases.Add(new TitleRelease
        {
            Id = foreignReleaseId,
            TitleId = secondTitleId,
            MetadataVersionId = secondMetadataId,
            Version = "1.0.0",
            Status = "published",
            PublishedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var firstTitle = await dbContext.Titles.SingleAsync(candidate => candidate.Id == firstTitleId);
        firstTitle.CurrentReleaseId = foreignReleaseId;

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Schema_WithCrossTitleMetadataVersionInRelease_RejectsInvalidReference()
    {
        var organizationId = Guid.NewGuid();
        var firstTitleId = Guid.NewGuid();
        var secondTitleId = Guid.NewGuid();
        var firstMetadataId = Guid.NewGuid();
        var secondMetadataId = Guid.NewGuid();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
        await SeedTitleWithMetadataAsync(dbContext, organizationId, firstTitleId, firstMetadataId, "star-blasters");
        await SeedTitleWithMetadataAsync(dbContext, organizationId, secondTitleId, secondMetadataId, "puzzle-grove");

        dbContext.TitleReleases.Add(new TitleRelease
        {
            Id = Guid.NewGuid(),
            TitleId = firstTitleId,
            MetadataVersionId = secondMetadataId,
            Version = "1.0.0",
            Status = "draft",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Schema_WithDuplicateArtifactIdentity_RejectsSecondArtifact()
    {
        var organizationId = Guid.NewGuid();
        var titleId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();
        var releaseId = Guid.NewGuid();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
        await SeedTitleWithMetadataAsync(dbContext, organizationId, titleId, metadataId);

        dbContext.TitleReleases.Add(new TitleRelease
        {
            Id = releaseId,
            TitleId = titleId,
            MetadataVersionId = metadataId,
            Version = "1.0.0",
            Status = "draft",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        dbContext.ReleaseArtifacts.Add(new ReleaseArtifact
        {
            Id = Guid.NewGuid(),
            ReleaseId = releaseId,
            ArtifactKind = "apk",
            PackageName = "fun.board.starblasters",
            VersionCode = 100,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        dbContext.ReleaseArtifacts.Add(new ReleaseArtifact
        {
            Id = Guid.NewGuid(),
            ReleaseId = releaseId,
            ArtifactKind = "apk",
            PackageName = "fun.board.starblasters",
            VersionCode = 100,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task UpdateReleaseArtifactEndpoint_WithDuplicateIdentity_ReturnsConflict()
    {
        await using (var migrationContext = CreateDbContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        var organizationId = Guid.NewGuid();
        var editorUserId = Guid.NewGuid();
        var titleId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();
        var releaseId = Guid.NewGuid();
        var firstArtifactId = Guid.NewGuid();
        var secondArtifactId = Guid.NewGuid();

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
                LifecycleStatus = "draft",
                Visibility = "private",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            seedContext.TitleMetadataVersions.Add(new TitleMetadataVersion
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
            seedContext.TitleReleases.Add(new TitleRelease
            {
                Id = releaseId,
                TitleId = titleId,
                MetadataVersionId = metadataId,
                Version = "1.0.0",
                Status = "draft",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            seedContext.ReleaseArtifacts.AddRange(
                new ReleaseArtifact
                {
                    Id = firstArtifactId,
                    ReleaseId = releaseId,
                    ArtifactKind = "apk",
                    PackageName = "fun.board.starblasters",
                    VersionCode = 100,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new ReleaseArtifact
                {
                    Id = secondArtifactId,
                    ReleaseId = releaseId,
                    ArtifactKind = "apk",
                    PackageName = "fun.board.puzzlegrove",
                    VersionCode = 200,
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

        using var response = await client.PutAsJsonAsync(
            $"/developer/titles/{titleId}/releases/{releaseId}/artifacts/{firstArtifactId}",
            new
            {
                artifactKind = "apk",
                packageName = "fun.board.puzzlegrove",
                versionCode = 200L,
                sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                fileSizeBytes = 4096L
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("release_artifact_identity_conflict", document.RootElement.GetProperty("code").GetString());
    }

    private async Task SeedTitleAsync(BoardLibraryDbContext dbContext, Guid organizationId, Guid titleId, string slug = "star-blasters")
    {
        if (!await dbContext.Organizations.AnyAsync(candidate => candidate.Id == organizationId))
        {
            dbContext.Organizations.Add(new Organization
            {
                Id = organizationId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }

        dbContext.Titles.Add(new Title
        {
            Id = titleId,
            OrganizationId = organizationId,
            Slug = slug,
            ContentKind = "game",
            LifecycleStatus = "draft",
            Visibility = "private",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedTitleWithMetadataAsync(BoardLibraryDbContext dbContext, Guid organizationId, Guid titleId, Guid metadataId, string slug = "star-blasters")
    {
        await SeedTitleAsync(dbContext, organizationId, titleId, slug);
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
