using System.Net;
using System.Net.Http.Headers;
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
/// Integration tests for the Wave 2 studio persistence model and endpoints.
/// </summary>
[Trait("Category", "Integration")]
public sealed class StudioPersistenceIntegrationTests : IAsyncLifetime
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
    /// Verifies studio create, public read, membership listing, and delete all work against real PostgreSQL.
    /// </summary>
    [Fact]
    public async Task StudioEndpoints_WithRealPostgres_RoundTripPersistedData()
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
            "/studios",
            new
            {
                slug = "stellar-forge",
                displayName = "Stellar Forge",
                description = "Family co-op studio.",
                logoUrl = "https://cdn.example.com/orgs/stellar-forge.png",
                bannerUrl = "https://cdn.example.com/orgs/stellar-forge-banner.png"
            });
        var createPayload = await createResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(createPayload);
        var studioId = Guid.Parse(createDocument.RootElement.GetProperty("studio").GetProperty("id").GetString()!);
        Assert.Equal("https://cdn.example.com/orgs/stellar-forge-banner.png", createDocument.RootElement.GetProperty("studio").GetProperty("bannerUrl").GetString());

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
            $"/developer/studios/{studioId}/memberships/editor-456",
            new
            {
                role = "editor"
            });

        Assert.Equal(HttpStatusCode.OK, membershipResponse.StatusCode);

        using var createLinkResponse = await client.PostAsJsonAsync(
            $"/developer/studios/{studioId}/links",
            new
            {
                label = "Discord",
                url = "https://discord.gg/stellarforge"
            });

        Assert.Equal(HttpStatusCode.Created, createLinkResponse.StatusCode);

        using var uploadContent = new MultipartFormDataContent();
        using var mediaContent = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
        mediaContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        uploadContent.Add(mediaContent, "media", "logo.png");

        using var uploadResponse = await client.PostAsync($"/developer/studios/{studioId}/logo-upload", uploadContent);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        using var publicGetResponse = await client.GetAsync("/studios/stellar-forge");
        var publicGetPayload = await publicGetResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, publicGetResponse.StatusCode);

        using var publicGetDocument = JsonDocument.Parse(publicGetPayload);
        Assert.Equal(
            "Stellar Forge",
            publicGetDocument.RootElement.GetProperty("studio").GetProperty("displayName").GetString());
        Assert.Single(publicGetDocument.RootElement.GetProperty("studio").GetProperty("links").EnumerateArray());
        Assert.StartsWith("http://localhost/uploads/studio-media/", publicGetDocument.RootElement.GetProperty("studio").GetProperty("logoUrl").GetString(), StringComparison.Ordinal);
        Assert.Equal("https://cdn.example.com/orgs/stellar-forge-banner.png", publicGetDocument.RootElement.GetProperty("studio").GetProperty("bannerUrl").GetString());

        using var membershipsGetResponse = await client.GetAsync($"/developer/studios/{studioId}/memberships");
        var membershipsPayload = await membershipsGetResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, membershipsGetResponse.StatusCode);

        using var membershipsDocument = JsonDocument.Parse(membershipsPayload);
        Assert.Equal(2, membershipsDocument.RootElement.GetProperty("memberships").GetArrayLength());

        using var deleteResponse = await client.DeleteAsync($"/developer/studios/{studioId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await using var verificationContext = CreateDbContext();
        Assert.False(await verificationContext.Studios.AnyAsync(candidate => candidate.Id == studioId));
        Assert.False(await verificationContext.StudioMemberships.AnyAsync(candidate => candidate.StudioId == studioId));
        Assert.False(await verificationContext.StudioLinks.AnyAsync(candidate => candidate.StudioId == studioId));
    }

    /// <summary>
    /// Verifies studio link CRUD persists against real PostgreSQL.
    /// </summary>
    [Fact]
    public async Task StudioLinksEndpoints_WithRealPostgres_RoundTripPersistedData()
    {
        await using (var migrationContext = CreateDbContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        var ownerUserId = Guid.NewGuid();
        var studioId = Guid.NewGuid();

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
            seedContext.Studios.Add(new Studio
            {
                Id = studioId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            seedContext.StudioMemberships.Add(new StudioMembership
            {
                StudioId = studioId,
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

        using var createResponse = await client.PostAsJsonAsync(
            $"/developer/studios/{studioId}/links",
            new
            {
                label = "Discord",
                url = "https://discord.gg/stellarforge"
            });
        var createPayload = await createResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(createPayload);
        var linkId = Guid.Parse(createDocument.RootElement.GetProperty("link").GetProperty("id").GetString()!);

        using var updateResponse = await client.PutAsJsonAsync(
            $"/developer/studios/{studioId}/links/{linkId}",
            new
            {
                label = "Community Discord",
                url = "https://discord.gg/stellarforgehq"
            });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var listResponse = await client.GetAsync($"/developer/studios/{studioId}/links");
        var listPayload = await listResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using (var listDocument = JsonDocument.Parse(listPayload))
        {
            var links = listDocument.RootElement.GetProperty("links").EnumerateArray().ToArray();
            Assert.Single(links);
            Assert.Equal("Community Discord", links[0].GetProperty("label").GetString());
        }

        using var deleteResponse = await client.DeleteAsync($"/developer/studios/{studioId}/links/{linkId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await using var verificationContext = CreateDbContext();
        Assert.False(await verificationContext.StudioLinks.AnyAsync(candidate => candidate.Id == linkId));
    }

    /// <summary>
    /// Verifies the schema rejects duplicate studio slugs.
    /// </summary>
    [Fact]
    public async Task Schema_WithDuplicateStudioSlug_RejectsSecondStudio()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();

        dbContext.Studios.Add(new Studio
        {
            Id = Guid.NewGuid(),
            Slug = "stellar-forge",
            DisplayName = "Stellar Forge",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();

        dbContext.Studios.Add(new Studio
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
    /// Verifies the schema rejects duplicate memberships for the same studio and user.
    /// </summary>
    [Fact]
    public async Task Schema_WithDuplicateStudioMembership_RejectsSecondMembership()
    {
        Guid studioId;
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
            var studio = new Studio
            {
                Id = Guid.NewGuid(),
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            studioId = studio.Id;
            userId = user.Id;

            dbContext.Users.Add(user);
            dbContext.Studios.Add(studio);
            await dbContext.SaveChangesAsync();

            dbContext.StudioMemberships.Add(new StudioMembership
            {
                StudioId = studioId,
                UserId = userId,
                Role = "owner",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync();
        }

        await using var verificationContext = CreateDbContext();
        verificationContext.StudioMemberships.Add(new StudioMembership
        {
            StudioId = studioId,
            UserId = userId,
            Role = "admin",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => verificationContext.SaveChangesAsync());
    }

    /// <summary>
    /// Verifies duplicate studio creation returns the public conflict payload against PostgreSQL.
    /// </summary>
    [Fact]
    public async Task CreateStudioEndpoint_WithDuplicateSlug_ReturnsConflict()
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
            "/studios",
            new
            {
                slug = "stellar-forge",
                displayName = "Stellar Forge"
            });
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var secondResponse = await client.PostAsJsonAsync(
            "/studios",
            new
            {
                slug = "stellar-forge",
                displayName = "Duplicate Stellar Forge"
            });
        var payload = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("studio_slug_conflict", document.RootElement.GetProperty("code").GetString());
    }

    /// <summary>
    /// Verifies studio updates return the public conflict payload when a duplicate slug is requested.
    /// </summary>
    [Fact]
    public async Task UpdateStudioEndpoint_WithDuplicateSlug_ReturnsConflict()
    {
        await using (var migrationContext = CreateDbContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        var ownerUserId = Guid.NewGuid();
        var firstStudioId = Guid.NewGuid();
        var secondStudioId = Guid.NewGuid();

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
            seedContext.Studios.AddRange(
                new Studio
                {
                    Id = firstStudioId,
                    Slug = "stellar-forge",
                    DisplayName = "Stellar Forge",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new Studio
                {
                    Id = secondStudioId,
                    Slug = "tabletop-sparks",
                    DisplayName = "Tabletop Sparks",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            seedContext.StudioMemberships.Add(new StudioMembership
            {
                StudioId = secondStudioId,
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
            $"/developer/studios/{secondStudioId}",
            new
            {
                slug = "stellar-forge",
                displayName = "Tabletop Sparks"
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("studio_slug_conflict", document.RootElement.GetProperty("code").GetString());
    }

    /// <summary>
    /// Verifies renamed migration identifiers are normalized for existing local databases before migrations run.
    /// </summary>
    [Fact]
    public async Task MigrationHistoryCompatibility_WithLegacyRenamedMigrations_RewritesIds()
    {
        await using (var dbContext = CreateDbContext())
        {
            await dbContext.Database.MigrateAsync();

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                UPDATE "__EFMigrationsHistory"
                SET "MigrationId" = '20260301204029_Wave2OrganizationsMemberships'
                WHERE "MigrationId" = '20260301204029_Wave2StudiosMemberships';
                """);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                UPDATE "__EFMigrationsHistory"
                SET "MigrationId" = '20260306031131_Wave7RenameOrganizationTablesToStudios'
                WHERE "MigrationId" = '20260306031131_Wave7RenameStudioTablesToStudios';
                """);
        }

        await using (var dbContext = CreateDbContext())
        {
            await MigrationHistoryCompatibility.NormalizeAsync(dbContext);

            var migrationIds = (await dbContext.Database.GetAppliedMigrationsAsync()).ToArray();

            Assert.Contains("20260301204029_Wave2StudiosMemberships", migrationIds);
            Assert.Contains("20260306031131_Wave7RenameStudioTablesToStudios", migrationIds);
            Assert.DoesNotContain("20260301204029_Wave2OrganizationsMemberships", migrationIds);
            Assert.DoesNotContain("20260306031131_Wave7RenameOrganizationTablesToStudios", migrationIds);

            await dbContext.Database.MigrateAsync();
        }
    }

    /// <summary>
    /// Verifies legacy organization-era schema object names are normalized to studio-era names before use.
    /// </summary>
    [Fact]
    public async Task LegacySchemaCompatibility_WithLegacyOrganizationNames_RewritesSchemaObjects()
    {
        await using (var dbContext = CreateDbContext())
        {
            await dbContext.Database.MigrateAsync();

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                ALTER TABLE public.studio_memberships RENAME COLUMN studio_id TO organization_id;
                ALTER TABLE public.titles RENAME COLUMN studio_id TO organization_id;
                ALTER TABLE public.integration_connections RENAME COLUMN studio_id TO organization_id;
                ALTER TABLE public.studios RENAME CONSTRAINT pk_studios TO pk_organizations;
                ALTER INDEX public.ux_studios_slug RENAME TO ux_organizations_slug;
                ALTER TABLE public.studio_memberships RENAME CONSTRAINT pk_studio_memberships TO pk_organization_memberships;
                ALTER TABLE public.studio_memberships RENAME CONSTRAINT ck_studio_memberships_role TO ck_organization_memberships_role;
                ALTER TABLE public.studio_memberships RENAME CONSTRAINT fk_studio_memberships_studios_studio_id TO fk_organization_memberships_organizations_organization_id;
                ALTER TABLE public.studio_memberships RENAME CONSTRAINT fk_studio_memberships_users_user_id TO fk_organization_memberships_users_user_id;
                ALTER INDEX public.ix_studio_memberships_user_id RENAME TO ix_organization_memberships_user_id;
                ALTER TABLE public.titles RENAME CONSTRAINT fk_titles_studios_studio_id TO fk_titles_organizations_organization_id;
                ALTER INDEX public.ix_titles_studio_id RENAME TO ix_titles_organization_id;
                ALTER INDEX public.ux_titles_studio_id_slug RENAME TO ux_titles_organization_id_slug;
                ALTER TABLE public.integration_connections RENAME CONSTRAINT fk_integration_connections_studios_studio_id TO fk_integration_connections_organizations_organization_id;
                ALTER INDEX public.ix_integration_connections_studio_id RENAME TO ix_integration_connections_organization_id;
                """);

            await LegacySchemaCompatibility.NormalizeAsync(dbContext);
            await dbContext.Database.MigrateAsync();
        }

        await using (var verificationContext = CreateDbContext())
        {
            Assert.True(await SchemaObjectExistsAsync(
                verificationContext,
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'studio_memberships' AND column_name = 'studio_id');
                """));
            Assert.True(await SchemaObjectExistsAsync(
                verificationContext,
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'titles' AND column_name = 'studio_id');
                """));
            Assert.True(await SchemaObjectExistsAsync(
                verificationContext,
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'integration_connections' AND column_name = 'studio_id');
                """));
            Assert.False(await SchemaObjectExistsAsync(
                verificationContext,
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public' AND column_name = 'organization_id'
                      AND table_name IN ('studio_memberships', 'titles', 'integration_connections'));
                """));

            Assert.True(await SchemaObjectExistsAsync(
                verificationContext,
                """
                SELECT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE connamespace = 'public'::regnamespace AND conname = 'fk_studio_memberships_studios_studio_id');
                """));
            Assert.True(await SchemaObjectExistsAsync(
                verificationContext,
                """
                SELECT EXISTS (
                    SELECT 1 FROM pg_indexes
                    WHERE schemaname = 'public' AND indexname = 'ux_studios_slug');
                """));
            Assert.False(await SchemaObjectExistsAsync(
                verificationContext,
                """
                SELECT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE connamespace = 'public'::regnamespace AND conname = 'fk_titles_organizations_organization_id');
                """));
        }
    }

    private BoardLibraryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BoardLibraryDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        return new BoardLibraryDbContext(options);
    }

    private static async Task<bool> SchemaObjectExistsAsync(BoardLibraryDbContext dbContext, string sql)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        return result is bool exists && exists;
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

