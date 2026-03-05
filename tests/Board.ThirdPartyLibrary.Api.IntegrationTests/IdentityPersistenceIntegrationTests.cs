using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Board.ThirdPartyLibrary.Api.Auth;
using Board.ThirdPartyLibrary.Api.Identity;
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
/// Integration tests for the Wave 1 identity projection and Board profile persistence.
/// </summary>
[Trait("Category", "Integration")]
public sealed class IdentityPersistenceIntegrationTests : IAsyncLifetime
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
    /// Verifies the current-user endpoint persists a local user projection in PostgreSQL.
    /// </summary>
    [Fact]
    public async Task CurrentUserEndpoint_WithRealPostgres_PersistsUserProjection()
    {
        await using (var migrationContext = CreateDbContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        using var factory = new RealPostgresApiFactory(
            _postgresContainer.GetConnectionString(),
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

        await using var dbContext = CreateDbContext();
        var user = await dbContext.Users.SingleAsync(candidate => candidate.KeycloakSubject == "user-123");

        Assert.Equal("Local Admin", user.DisplayName);
        Assert.Equal("admin@boardtpl.local", user.Email);
        Assert.True(user.EmailVerified);
        Assert.Equal("google", user.IdentityProvider);
    }

    /// <summary>
    /// Verifies profile and avatar endpoints persist application-managed profile fields in PostgreSQL.
    /// </summary>
    [Fact]
    public async Task CurrentUserProfileEndpoints_WithRealPostgres_RoundTripPersistedData()
    {
        await using (var migrationContext = CreateDbContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        using var factory = new RealPostgresApiFactory(
            _postgresContainer.GetConnectionString(),
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

        using var updateResponse = await client.PutAsJsonAsync(
            "/identity/me/profile",
            new
            {
                displayName = "Board Enthusiast"
            });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var avatarContent = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
        avatarContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        using var uploadContent = new MultipartFormDataContent
        {
            { avatarContent, "Avatar", "avatar.png" }
        };

        using var uploadResponse = await client.PostAsync("/identity/me/profile/avatar-upload", uploadContent);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        using var getResponse = await client.GetAsync("/identity/me/profile");
        var payload = await getResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var profile = document.RootElement.GetProperty("profile");
        Assert.Equal("local-admin", profile.GetProperty("userName").GetString());
        Assert.Equal("Local", profile.GetProperty("firstName").GetString());
        Assert.Equal("Admin", profile.GetProperty("lastName").GetString());
        Assert.StartsWith("data:image/png;base64,", profile.GetProperty("avatarDataUrl").GetString(), StringComparison.Ordinal);

        await using var dbContext = CreateDbContext();
        var user = await dbContext.Users.SingleAsync(candidate => candidate.KeycloakSubject == "user-123");
        Assert.Equal("Board Enthusiast", user.DisplayName);
        Assert.Equal("local-admin", user.UserName);
        Assert.Equal("Local", user.FirstName);
        Assert.Equal("Admin", user.LastName);
        Assert.Equal("image/png", user.AvatarImageContentType);
        Assert.NotNull(user.AvatarImageData);
    }

    /// <summary>
    /// Verifies username projections refresh from Keycloak claims when a user changes username.
    /// </summary>
    [Fact]
    public async Task CurrentUserProfileEndpoint_WithChangedPreferredUsername_RefreshesProjection()
    {
        await using (var migrationContext = CreateDbContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        using (var initialFactory = new RealPostgresApiFactory(
            _postgresContainer.GetConnectionString(),
            [
                new Claim("sub", "user-123"),
                new Claim("preferred_username", "local-admin"),
                new Claim("given_name", "Local"),
                new Claim("family_name", "Admin"),
                new Claim("email", "admin@boardtpl.local"),
                new Claim("email_verified", "true"),
                new Claim(ClaimTypes.Role, "player")
            ]))
        using (var initialClient = initialFactory.CreateClient())
        {
            using var initialResponse = await initialClient.GetAsync("/identity/me/profile");
            Assert.Equal(HttpStatusCode.OK, initialResponse.StatusCode);
        }

        using (var updatedFactory = new RealPostgresApiFactory(
            _postgresContainer.GetConnectionString(),
            [
                new Claim("sub", "user-123"),
                new Claim("preferred_username", "mattstromandev"),
                new Claim("given_name", "Matt"),
                new Claim("family_name", "Stroman"),
                new Claim("email", "admin@boardtpl.local"),
                new Claim("email_verified", "true"),
                new Claim(ClaimTypes.Role, "player")
            ]))
        using (var updatedClient = updatedFactory.CreateClient())
        {
            using var updatedResponse = await updatedClient.GetAsync("/identity/me/profile");
            var payload = await updatedResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);

            using var document = JsonDocument.Parse(payload);
            var profile = document.RootElement.GetProperty("profile");
            Assert.Equal("mattstromandev", profile.GetProperty("userName").GetString());
            Assert.Equal("Matt", profile.GetProperty("firstName").GetString());
            Assert.Equal("Stroman", profile.GetProperty("lastName").GetString());
        }

        await using var dbContext = CreateDbContext();
        var user = await dbContext.Users.SingleAsync(candidate => candidate.KeycloakSubject == "user-123");
        Assert.Equal("mattstromandev", user.UserName);
        Assert.Equal("Matt", user.FirstName);
        Assert.Equal("Stroman", user.LastName);
    }

    /// <summary>
    /// Verifies Board profile CRUD endpoints persist, round-trip, and delete data in PostgreSQL.
    /// </summary>
    [Fact]
    public async Task BoardProfileEndpoints_WithRealPostgres_RoundTripPersistedData()
    {
        await using (var migrationContext = CreateDbContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        using var factory = new RealPostgresApiFactory(
            _postgresContainer.GetConnectionString(),
            [
                new Claim("sub", "user-123"),
                new Claim("preferred_username", "local-admin")
            ]);
        using var client = factory.CreateClient();

        using var putResponse = await client.PutAsJsonAsync(
            "/identity/me/board-profile",
            new
            {
                boardUserId = "board_user_12345",
                displayName = "BoardKiddo",
                avatarUrl = "https://cdn.board.fun/users/board_user_12345/avatar.png"
            });
        var putPayload = await putResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        using var putDocument = JsonDocument.Parse(putPayload);
        Assert.Equal(
            "board_user_12345",
            putDocument.RootElement.GetProperty("boardProfile").GetProperty("boardUserId").GetString());

        using var getResponse = await client.GetAsync("/identity/me/board-profile");
        var getPayload = await getResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        using var getDocument = JsonDocument.Parse(getPayload);
        Assert.Equal(
            "BoardKiddo",
            getDocument.RootElement.GetProperty("boardProfile").GetProperty("displayName").GetString());

        using var deleteResponse = await client.DeleteAsync("/identity/me/board-profile");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var notFoundResponse = await client.GetAsync("/identity/me/board-profile");
        Assert.Equal(HttpStatusCode.NotFound, notFoundResponse.StatusCode);

        await using var dbContext = CreateDbContext();
        var user = await dbContext.Users.Include(candidate => candidate.BoardProfile)
            .SingleAsync(candidate => candidate.KeycloakSubject == "user-123");

        Assert.Null(user.BoardProfile);
    }

    /// <summary>
    /// Verifies submitting developer enrollment persists a pending request in PostgreSQL.
    /// </summary>
    [Fact]
    public async Task DeveloperEnrollmentEndpoint_WithRealPostgres_PersistsPendingRequest()
    {
        await using (var migrationContext = CreateDbContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        using var factory = new RealPostgresApiFactory(
            _postgresContainer.GetConnectionString(),
            [
                new Claim("sub", "user-456"),
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
        Assert.Equal("pending_review", document.RootElement.GetProperty("developerEnrollment").GetProperty("status").GetString());
        Assert.False(document.RootElement.GetProperty("developerEnrollment").GetProperty("developerAccessEnabled").GetBoolean());

        await using var dbContext = CreateDbContext();
        var user = await dbContext.Users.SingleAsync(candidate => candidate.KeycloakSubject == "user-456");
        var request = await dbContext.DeveloperEnrollmentRequests.SingleAsync(candidate => candidate.UserId == user.Id);
        Assert.Equal("Player One", user.DisplayName);
        Assert.Equal(DeveloperEnrollmentStatuses.Pending, request.Status);
        Assert.Null(request.ReviewedAtUtc);
    }

    /// <summary>
    /// Verifies moderator approval persists the approved state in PostgreSQL.
    /// </summary>
    [Fact]
    public async Task ModeratorApproveEnrollmentEndpoint_WithRealPostgres_PersistsApprovedRequest()
    {
        await using (var migrationContext = CreateDbContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        var roleClient = new StubKeycloakUserRoleClient(KeycloakUserRoleAssignmentResult.Success(alreadyAssigned: false));

        using (var seedContext = CreateDbContext())
        {
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

            seedContext.Users.Add(applicant);
            seedContext.ConversationThreads.Add(thread);
            seedContext.DeveloperEnrollmentRequests.Add(new DeveloperEnrollmentRequest
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
            await seedContext.SaveChangesAsync();
        }

        using var factory = new RealPostgresApiFactory(
            _postgresContainer.GetConnectionString(),
            [
                new Claim("sub", "moderator-123"),
                new Claim("name", "Moderator User"),
                new Claim(ClaimTypes.Role, "moderator")
            ],
            configureServices: services =>
            {
                services.RemoveAll<IKeycloakUserRoleClient>();
                services.AddSingleton<IKeycloakUserRoleClient>(roleClient);
            });
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/moderation/developer-enrollment-requests/2c54f9bb-1fdf-48e5-8cf0-a8b77f6174af/approve", null);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal(
            "approved",
            document.RootElement.GetProperty("developerEnrollmentRequest").GetProperty("status").GetString());

        await using var dbContext = CreateDbContext();
        var request = await dbContext.DeveloperEnrollmentRequests.SingleAsync(
            candidate => candidate.Id == Guid.Parse("2c54f9bb-1fdf-48e5-8cf0-a8b77f6174af"));

        Assert.Equal(DeveloperEnrollmentStatuses.Approved, request.Status);
        Assert.NotNull(request.ReviewedByUserId);
        Assert.NotNull(request.ReviewedAtUtc);
    }

    /// <summary>
    /// Verifies the information-request workflow persists conversation messages, attachments, and notifications in PostgreSQL.
    /// </summary>
    [Fact]
    public async Task DeveloperEnrollmentInformationRequestWorkflow_WithRealPostgres_PersistsConversationAndNotifications()
    {
        await using (var migrationContext = CreateDbContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        var requestId = Guid.Parse("2c54f9bb-1fdf-48e5-8cf0-a8b77f6174af");

        using (var seedContext = CreateDbContext())
        {
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

            seedContext.Users.Add(applicant);
            seedContext.ConversationThreads.Add(thread);
            seedContext.DeveloperEnrollmentRequests.Add(new DeveloperEnrollmentRequest
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
            await seedContext.SaveChangesAsync();
        }

        using (var moderatorFactory = new RealPostgresApiFactory(
            _postgresContainer.GetConnectionString(),
            [
                new Claim("sub", "moderator-123"),
                new Claim("name", "Moderator User"),
                new Claim(ClaimTypes.Role, "moderator")
            ]))
        using (var moderatorClient = moderatorFactory.CreateClient())
        {
            var moderatorAttachment = new ByteArrayContent("notes"u8.ToArray());
            moderatorAttachment.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
            using var requestMoreInfoContent = new MultipartFormDataContent
            {
                { new StringContent("Please attach release notes and portfolio links."), "Message" },
                { moderatorAttachment, "Attachments", "release-notes.txt" }
            };

            using var response = await moderatorClient.PostAsync(
                $"/moderation/developer-enrollment-requests/{requestId}/request-more-information",
                requestMoreInfoContent);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using (var applicantFactory = new RealPostgresApiFactory(
            _postgresContainer.GetConnectionString(),
            [
                new Claim("sub", "player-123"),
                new Claim("name", "Player One"),
                new Claim(ClaimTypes.Role, "player")
            ]))
        using (var applicantClient = applicantFactory.CreateClient())
        {
            var applicantAttachment = new ByteArrayContent("reply"u8.ToArray());
            applicantAttachment.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
            using var replyContent = new MultipartFormDataContent
            {
                { new StringContent("Attached are the release notes and links."), "Message" },
                { applicantAttachment, "Attachments", "reply.txt" }
            };

            using var response = await applicantClient.PostAsync(
                $"/identity/me/developer-enrollment/{requestId}/messages",
                replyContent);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await using var dbContext = CreateDbContext();
        var request = await dbContext.DeveloperEnrollmentRequests
            .Include(candidate => candidate.ConversationThread)
            .ThenInclude(thread => thread.Messages)
            .ThenInclude(message => message.Attachments)
            .Include(candidate => candidate.User)
            .SingleAsync(candidate => candidate.Id == requestId);

        Assert.Equal(DeveloperEnrollmentStatuses.Pending, request.Status);
        Assert.Equal(2, request.ConversationThread.Messages.Count);
        Assert.Equal(2, request.ConversationThread.Messages.SelectMany(candidate => candidate.Attachments).Count());

        var applicantUser = await dbContext.Users.SingleAsync(candidate => candidate.KeycloakSubject == "player-123");
        var moderatorUser = await dbContext.Users.SingleAsync(candidate => candidate.KeycloakSubject == "moderator-123");
        var applicantNotifications = await dbContext.UserNotifications.CountAsync(candidate => candidate.UserId == applicantUser.Id);
        var moderatorNotifications = await dbContext.UserNotifications.CountAsync(candidate => candidate.UserId == moderatorUser.Id);

        Assert.Equal(1, applicantNotifications);
        Assert.Equal(1, moderatorNotifications);
    }

    /// <summary>
    /// Verifies marking a notification read persists read state in PostgreSQL.
    /// </summary>
    [Fact]
    public async Task MarkNotificationReadEndpoint_WithRealPostgres_PersistsReadState()
    {
        await using (var migrationContext = CreateDbContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        var notificationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        using (var seedContext = CreateDbContext())
        {
            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "player-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            seedContext.Users.Add(user);
            seedContext.UserNotifications.Add(new UserNotification
            {
                Id = notificationId,
                UserId = user.Id,
                Category = NotificationCategories.DeveloperEnrollment,
                Title = "Unread notification",
                Body = "Still unread.",
                IsRead = false,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            });
            await seedContext.SaveChangesAsync();
        }

        using var factory = new RealPostgresApiFactory(
            _postgresContainer.GetConnectionString(),
            [
                new Claim("sub", "player-123"),
                new Claim(ClaimTypes.Role, "player")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync($"/identity/me/notifications/{notificationId}/read", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var dbContext = CreateDbContext();
        var persisted = await dbContext.UserNotifications.SingleAsync(candidate => candidate.Id == notificationId);
        Assert.True(persisted.IsRead);
        Assert.NotNull(persisted.ReadAtUtc);
    }

    /// <summary>
    /// Verifies the schema rejects duplicate Keycloak subject values.
    /// </summary>
    [Fact]
    public async Task Schema_WithDuplicateKeycloakSubject_RejectsSecondUser()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();

        dbContext.Users.Add(new AppUser
        {
            Id = Guid.NewGuid(),
            KeycloakSubject = "duplicate-subject",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();

        dbContext.Users.Add(new AppUser
        {
            Id = Guid.NewGuid(),
            KeycloakSubject = "duplicate-subject",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    /// <summary>
    /// Verifies the schema rejects linking the same Board user ID to multiple application users.
    /// </summary>
    [Fact]
    public async Task Schema_WithDuplicateBoardUserId_RejectsSecondProfile()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();

        var firstUser = new AppUser
        {
            Id = Guid.NewGuid(),
            KeycloakSubject = "subject-1",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        var secondUser = new AppUser
        {
            Id = Guid.NewGuid(),
            KeycloakSubject = "subject-2",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        dbContext.Users.AddRange(firstUser, secondUser);
        await dbContext.SaveChangesAsync();

        dbContext.UserBoardProfiles.Add(new UserBoardProfile
        {
            UserId = firstUser.Id,
            BoardUserId = "board_user_12345",
            DisplayName = "BoardKiddo",
            LinkedAtUtc = DateTime.UtcNow,
            LastSyncedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();

        dbContext.UserBoardProfiles.Add(new UserBoardProfile
        {
            UserId = secondUser.Id,
            BoardUserId = "board_user_12345",
            DisplayName = "AnotherBoardKiddo",
            LinkedAtUtc = DateTime.UtcNow,
            LastSyncedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
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
        private readonly Action<IServiceCollection>? _configureServices;

        public RealPostgresApiFactory(
            string connectionString,
            IReadOnlyList<Claim> claims,
            Action<IServiceCollection>? configureServices = null)
        {
            _connectionString = connectionString;
            _claims = claims;
            _configureServices = configureServices;
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

                services.RemoveAll<IKeycloakUserRoleClient>();
                services.AddSingleton<IKeycloakUserRoleClient>(
                    new StubKeycloakUserRoleClient(KeycloakUserRoleAssignmentResult.Success(alreadyAssigned: false)));

                _configureServices?.Invoke(services);
            });
        }
    }

    private sealed class StubKeycloakUserRoleClient : IKeycloakUserRoleClient
    {
        private readonly KeycloakUserRoleAssignmentResult _result;

        public StubKeycloakUserRoleClient(KeycloakUserRoleAssignmentResult result)
        {
            _result = result;
        }

        public Task<KeycloakUserRoleAssignmentResult> EnsureRealmRoleAssignedAsync(
            string userSubject,
            string roleName,
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
