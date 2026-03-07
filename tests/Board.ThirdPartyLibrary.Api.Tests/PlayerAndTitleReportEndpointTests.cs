using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Board.ThirdPartyLibrary.Api.Persistence;
using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Board.ThirdPartyLibrary.Api.Players;
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
/// Endpoint tests for player-library and title-report workflows.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PlayerAndTitleReportEndpointTests
{
    [Fact]
    public async Task LibraryAndWishlistEndpoints_WithAuthenticatedPlayer_PersistAndReturnTitles()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "player-123"), new Claim(ClaimTypes.Role, "player")]);

        Guid titleId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            titleId = await SeedPublicTitleAsync(dbContext, "player-123");
        }

        using var client = factory.CreateClient();

        using var addLibraryResponse = await client.PutAsync($"/player/library/titles/{titleId}", null);
        using var addWishlistResponse = await client.PutAsync($"/player/wishlist/titles/{titleId}", null);
        using var libraryResponse = await client.GetAsync("/player/library");
        using var wishlistResponse = await client.GetAsync("/player/wishlist");

        Assert.Equal(HttpStatusCode.OK, addLibraryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, addWishlistResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, libraryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, wishlistResponse.StatusCode);

        using var libraryDocument = JsonDocument.Parse(await libraryResponse.Content.ReadAsStringAsync());
        using var wishlistDocument = JsonDocument.Parse(await wishlistResponse.Content.ReadAsStringAsync());
        Assert.Equal(titleId.ToString("D"), libraryDocument.RootElement.GetProperty("titles")[0].GetProperty("id").GetString());
        Assert.Equal(titleId.ToString("D"), wishlistDocument.RootElement.GetProperty("titles")[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task CreatePlayerReportEndpoint_WithAuthenticatedPlayer_PersistsAndReturnsReport()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "player-123"), new Claim(ClaimTypes.Role, "player")]);

        Guid titleId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            titleId = await SeedPublicTitleAsync(dbContext, "player-123");
        }

        using var client = factory.CreateClient();
        using var createResponse = await client.PostAsJsonAsync(
            "/player/reports",
            new
            {
                titleId,
                reason = "The title card is using misleading artwork."
            });
        var createPayload = await createResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(createPayload);
        Assert.Equal("open", createDocument.RootElement.GetProperty("report").GetProperty("status").GetString());

        using var reportListResponse = await client.GetAsync("/player/reports");
        Assert.Equal(HttpStatusCode.OK, reportListResponse.StatusCode);

        using var listDocument = JsonDocument.Parse(await reportListResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, listDocument.RootElement.GetProperty("reports").GetArrayLength());
    }

    [Fact]
    public async Task CreatePlayerReportEndpoint_WithBlankReason_ReturnsValidationProblem()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "player-123"), new Claim(ClaimTypes.Role, "player")]);

        Guid titleId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            titleId = await SeedPublicTitleAsync(dbContext, "player-123");
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/player/reports",
            new
            {
                titleId,
                reason = "   "
            });

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }

    [Fact]
    public async Task CreatePlayerReportEndpoint_WithExistingOpenReport_ReturnsConflict()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "player-123"), new Claim(ClaimTypes.Role, "player")]);

        Guid titleId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            titleId = await SeedPublicTitleAsync(dbContext, "player-123");
        }

        using var client = factory.CreateClient();
        using var firstResponse = await client.PostAsJsonAsync(
            "/player/reports",
            new
            {
                titleId,
                reason = "The title card is using misleading artwork."
            });
        using var secondResponse = await client.PostAsJsonAsync(
            "/player/reports",
            new
            {
                titleId,
                reason = "Submitting the same report again."
            });

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task CatalogEndpoints_WithOpenReport_ReturnReportedFlagForAllCallers()
    {
        using var factory = new TestApiFactory();

        string studioSlug;
        string titleSlug;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            (_, studioSlug, titleSlug) = await SeedOpenReportAsync(dbContext);
        }

        using var client = factory.CreateClient();

        using var browseResponse = await client.GetAsync($"/catalog?studioSlug={studioSlug}&pageNumber=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, browseResponse.StatusCode);

        using var browseDocument = JsonDocument.Parse(await browseResponse.Content.ReadAsStringAsync());
        Assert.True(browseDocument.RootElement.GetProperty("titles")[0].GetProperty("isReported").GetBoolean());

        using var detailResponse = await client.GetAsync($"/catalog/{studioSlug}/{titleSlug}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);

        using var detailDocument = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync());
        Assert.True(detailDocument.RootElement.GetProperty("title").GetProperty("isReported").GetBoolean());
    }

    [Fact]
    public async Task ModeratorValidateReportEndpoint_WithOpenReport_UnlistsTitleFromBrowseButKeepsDirectAccess()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "moderator-123"), new Claim(ClaimTypes.Role, "player"), new Claim(ClaimTypes.Role, "moderator")]);

        Guid reportId;
        string studioSlug;
        string titleSlug;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            (reportId, studioSlug, titleSlug) = await SeedOpenReportAsync(dbContext);
            dbContext.Users.Add(new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "moderator-123",
                DisplayName = "Moderator One",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var validateResponse = await client.PostAsJsonAsync(
            $"/moderation/title-reports/{reportId}/validate",
            new
            {
                note = "Validated during moderation review."
            });

        Assert.Equal(HttpStatusCode.OK, validateResponse.StatusCode);

        using var browseResponse = await client.GetAsync($"/catalog?studioSlug={studioSlug}&pageNumber=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, browseResponse.StatusCode);

        using var browseDocument = JsonDocument.Parse(await browseResponse.Content.ReadAsStringAsync());
        Assert.Equal(0, browseDocument.RootElement.GetProperty("titles").GetArrayLength());

        using var catalogResponse = await client.GetAsync($"/catalog/{studioSlug}/{titleSlug}");
        Assert.Equal(HttpStatusCode.OK, catalogResponse.StatusCode);

        using var catalogDocument = JsonDocument.Parse(await catalogResponse.Content.ReadAsStringAsync());
        Assert.Equal("unlisted", catalogDocument.RootElement.GetProperty("title").GetProperty("visibility").GetString());
        Assert.Equal("testing", catalogDocument.RootElement.GetProperty("title").GetProperty("lifecycleStatus").GetString());
    }

    [Fact]
    public async Task NotificationEndpoints_WithCurrentUserNotification_ListAndMarkRead()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "player-123"), new Claim(ClaimTypes.Role, "player")]);

        Guid notificationId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            var now = DateTime.UtcNow;
            var userId = Guid.NewGuid();
            notificationId = Guid.NewGuid();

            dbContext.Users.Add(new AppUser
            {
                Id = userId,
                KeycloakSubject = "player-123",
                DisplayName = "Player One",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            dbContext.UserNotifications.Add(new UserNotification
            {
                Id = notificationId,
                UserId = userId,
                Category = "title_report",
                Title = "Developer replied",
                Body = "Developer One replied on Star Blasters.",
                ActionUrl = "/moderate?workflow=reports-review&reportId=aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                IsRead = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();

        using var listResponse = await client.GetAsync("/identity/me/notifications");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using var listDocument = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, listDocument.RootElement.GetProperty("notifications").GetArrayLength());
        Assert.False(listDocument.RootElement.GetProperty("notifications")[0].GetProperty("isRead").GetBoolean());

        using var readResponse = await client.PostAsync($"/identity/me/notifications/{notificationId:D}/read", null);
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);

        using var readDocument = JsonDocument.Parse(await readResponse.Content.ReadAsStringAsync());
        Assert.True(readDocument.RootElement.GetProperty("notification").GetProperty("isRead").GetBoolean());
    }

    [Fact]
    public async Task CreatePlayerReportEndpoint_WithKnownModerators_CreatesModeratorNotification()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "player-123"), new Claim(ClaimTypes.Role, "player")]);

        Guid titleId;
        Guid moderatorUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            titleId = await SeedPublicTitleAsync(dbContext, "player-123");
            moderatorUserId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            dbContext.Users.Add(new AppUser
            {
                Id = moderatorUserId,
                KeycloakSubject = "moderator-123",
                DisplayName = "Moderator One",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            dbContext.UserPlatformRoles.Add(new UserPlatformRole
            {
                UserId = moderatorUserId,
                Role = "moderator",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var createResponse = await client.PostAsJsonAsync(
            "/player/reports",
            new
            {
                titleId,
                reason = "The title card is using misleading artwork."
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var notification = await verificationDbContext.UserNotifications.SingleAsync(candidate => candidate.UserId == moderatorUserId);
        Assert.Equal("title_report", notification.Category);
        Assert.Contains("/moderate?workflow=reports-review&reportId=", notification.ActionUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModeratorMessageEndpoint_WithPlayerRecipient_CreatesPlayerNotificationAndPlayerAudienceMessage()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "moderator-123"), new Claim(ClaimTypes.Role, "player"), new Claim(ClaimTypes.Role, "moderator")]);

        Guid reportId;
        Guid reporterUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            var context = await SeedDeveloperManagedReportContextAsync(dbContext, "developer-123");
            reportId = context.ReportId;
            reporterUserId = context.ReporterUserId;
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"/moderation/title-reports/{reportId}/messages",
            new
            {
                message = "Please share the exact steps and screenshots you saw in the title.",
                recipientRole = "player"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("needs_player_response", document.RootElement.GetProperty("report").GetProperty("report").GetProperty("status").GetString());
        Assert.Equal("player", document.RootElement.GetProperty("report").GetProperty("messages")[0].GetProperty("audience").GetString());

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var notification = await verificationDbContext.UserNotifications.SingleAsync(candidate => candidate.UserId == reporterUserId);
        Assert.Contains("/player?workflow=reported-titles&reportId=", notification.ActionUrl, StringComparison.Ordinal);

        var storedMessage = await verificationDbContext.TitleReportMessages.SingleAsync(candidate => candidate.TitleReportId == reportId);
        Assert.Equal("player", storedMessage.Audience);
    }

    [Fact]
    public async Task ModeratorMessageEndpoint_WithInvalidRecipientRole_ReturnsValidationProblem()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "moderator-123"), new Claim(ClaimTypes.Role, "player"), new Claim(ClaimTypes.Role, "moderator")]);

        Guid reportId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            var context = await SeedDeveloperManagedReportContextAsync(dbContext, "developer-123");
            reportId = context.ReportId;
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"/moderation/title-reports/{reportId}/messages",
            new
            {
                message = "Need more details.",
                recipientRole = "qa"
            });

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }

    [Fact]
    public async Task PlayerReportMessageEndpoint_WithOwnedReport_CreatesModeratorNotification()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "player-123"), new Claim(ClaimTypes.Role, "player")]);

        Guid reportId;
        Guid moderatorUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            (reportId, _, _) = await SeedOpenReportAsync(dbContext);
            moderatorUserId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            dbContext.Users.Add(new AppUser
            {
                Id = moderatorUserId,
                KeycloakSubject = "moderator-123",
                DisplayName = "Moderator One",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            dbContext.UserPlatformRoles.Add(new UserPlatformRole
            {
                UserId = moderatorUserId,
                Role = "moderator",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();

        using var getResponse = await client.GetAsync($"/player/reports/{reportId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        using var replyResponse = await client.PostAsJsonAsync(
            $"/player/reports/{reportId}/messages",
            new
            {
                message = "The issue is visible on the quick-view card and in the full description."
            });

        Assert.Equal(HttpStatusCode.OK, replyResponse.StatusCode);

        using var replyDocument = JsonDocument.Parse(await replyResponse.Content.ReadAsStringAsync());
        Assert.Equal("player_responded", replyDocument.RootElement.GetProperty("report").GetProperty("report").GetProperty("status").GetString());
        Assert.Equal("player", replyDocument.RootElement.GetProperty("report").GetProperty("messages")[0].GetProperty("audience").GetString());

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var notification = await verificationDbContext.UserNotifications.SingleAsync(candidate => candidate.UserId == moderatorUserId);
        Assert.Contains("/moderate?workflow=reports-review&reportId=", notification.ActionUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPlayerReportEndpoint_WithDifferentAuthenticatedPlayer_ReturnsForbidden()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "player-456"), new Claim(ClaimTypes.Role, "player")]);

        Guid reportId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            (reportId, _, _) = await SeedOpenReportAsync(dbContext);
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/player/reports/{reportId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetPlayerReportEndpoint_WithUnknownReport_ReturnsNotFound()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "player-123"), new Claim(ClaimTypes.Role, "player")]);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/player/reports/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PlayerReportMessageEndpoint_WithDifferentAuthenticatedPlayer_ReturnsForbidden()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "player-456"), new Claim(ClaimTypes.Role, "player")]);

        Guid reportId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            (reportId, _, _) = await SeedOpenReportAsync(dbContext);
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"/player/reports/{reportId}/messages",
            new
            {
                message = "This should not be allowed."
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ModeratorValidateReportEndpoint_WithDeveloperManagedReport_NotifiesDeveloperAndReporterAndStoresSharedResolutionMessage()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "moderator-123"), new Claim(ClaimTypes.Role, "player"), new Claim(ClaimTypes.Role, "moderator")]);

        Guid reportId;
        Guid developerUserId;
        Guid reporterUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            var context = await SeedDeveloperManagedReportContextAsync(dbContext, "developer-123");
            reportId = context.ReportId;
            developerUserId = context.DeveloperUserId;
            reporterUserId = context.ReporterUserId;
        }

        using var client = factory.CreateClient();
        using var validateResponse = await client.PostAsJsonAsync(
            $"/moderation/title-reports/{reportId}/validate",
            new
            {
                note = "Validated during moderation review."
            });

        Assert.Equal(HttpStatusCode.OK, validateResponse.StatusCode);

        using var validateDocument = JsonDocument.Parse(await validateResponse.Content.ReadAsStringAsync());
        Assert.Equal("validated", validateDocument.RootElement.GetProperty("report").GetProperty("report").GetProperty("status").GetString());
        Assert.Equal("all", validateDocument.RootElement.GetProperty("report").GetProperty("messages")[0].GetProperty("audience").GetString());

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var notificationRecipients = await verificationDbContext.UserNotifications
            .OrderBy(candidate => candidate.UserId)
            .Select(candidate => candidate.UserId)
            .ToArrayAsync();
        Assert.Equal(new[] { developerUserId, reporterUserId }.OrderBy(candidate => candidate).ToArray(), notificationRecipients);

        var storedMessage = await verificationDbContext.TitleReportMessages.SingleAsync(candidate => candidate.TitleReportId == reportId);
        Assert.Equal("all", storedMessage.Audience);
    }

    [Fact]
    public async Task DeveloperMessageEndpoint_WithManagedTitle_UpdatesReportStatus()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "developer-123"), new Claim(ClaimTypes.Role, "player"), new Claim(ClaimTypes.Role, "developer")]);

        Guid titleId;
        Guid reportId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            (titleId, reportId) = await SeedDeveloperManagedReportAsync(dbContext, "developer-123");
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"/developer/titles/{titleId}/reports/{reportId}/messages",
            new
            {
                message = "We are updating the card art and description now."
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("developer_responded", document.RootElement.GetProperty("report").GetProperty("report").GetProperty("status").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("report").GetProperty("messages").GetArrayLength());
    }

    private static async Task<Guid> SeedPublicTitleAsync(BoardLibraryDbContext dbContext, string playerSubject)
    {
        var now = DateTime.UtcNow;
        var studioId = Guid.NewGuid();
        var titleId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();

        dbContext.Users.Add(new AppUser
        {
            Id = Guid.NewGuid(),
            KeycloakSubject = playerSubject,
            DisplayName = "Player One",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.Studios.Add(new Studio
        {
            Id = studioId,
            Slug = "stellar-forge",
            DisplayName = "Stellar Forge",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.Titles.Add(new Title
        {
            Id = titleId,
            StudioId = studioId,
            Slug = "star-blasters",
            ContentKind = "game",
            LifecycleStatus = "testing",
            Visibility = "listed",
            CurrentMetadataVersionId = metadataId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
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
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await dbContext.SaveChangesAsync();
        return titleId;
    }

    private static async Task<(Guid ReportId, string StudioSlug, string TitleSlug)> SeedOpenReportAsync(BoardLibraryDbContext dbContext)
    {
        var now = DateTime.UtcNow;
        var reporterId = Guid.NewGuid();
        var studioId = Guid.NewGuid();
        var titleId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        const string studioSlug = "stellar-forge";
        const string titleSlug = "star-blasters";

        dbContext.Users.Add(new AppUser
        {
            Id = reporterId,
            KeycloakSubject = "player-123",
            DisplayName = "Player One",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.Studios.Add(new Studio
        {
            Id = studioId,
            Slug = studioSlug,
            DisplayName = "Stellar Forge",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.Titles.Add(new Title
        {
            Id = titleId,
            StudioId = studioId,
            Slug = titleSlug,
            ContentKind = "game",
            LifecycleStatus = "testing",
            Visibility = "listed",
            CurrentMetadataVersionId = metadataId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
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
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.TitleReports.Add(new TitleReport
        {
            Id = reportId,
            TitleId = titleId,
            ReporterUserId = reporterId,
            Status = TitleReportStatuses.Open,
            Reason = "Report reason.",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await dbContext.SaveChangesAsync();
        return (reportId, studioSlug, titleSlug);
    }

    private static async Task<(Guid TitleId, Guid ReportId)> SeedDeveloperManagedReportAsync(BoardLibraryDbContext dbContext, string developerSubject)
    {
        var context = await SeedDeveloperManagedReportContextAsync(dbContext, developerSubject);
        return (context.TitleId, context.ReportId);
    }

    private static async Task<(Guid TitleId, Guid ReportId, Guid DeveloperUserId, Guid ReporterUserId)> SeedDeveloperManagedReportContextAsync(BoardLibraryDbContext dbContext, string developerSubject)
    {
        var now = DateTime.UtcNow;
        var developerUserId = Guid.NewGuid();
        var reporterUserId = Guid.NewGuid();
        var studioId = Guid.NewGuid();
        var titleId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        dbContext.Users.AddRange(
            new AppUser
            {
                Id = developerUserId,
                KeycloakSubject = developerSubject,
                DisplayName = "Developer One",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new AppUser
            {
                Id = reporterUserId,
                KeycloakSubject = "player-123",
                DisplayName = "Player One",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        dbContext.Studios.Add(new Studio
        {
            Id = studioId,
            Slug = "stellar-forge",
            DisplayName = "Stellar Forge",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.StudioMemberships.Add(new StudioMembership
        {
            StudioId = studioId,
            UserId = developerUserId,
            Role = "editor",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.Titles.Add(new Title
        {
            Id = titleId,
            StudioId = studioId,
            Slug = "star-blasters",
            ContentKind = "game",
            LifecycleStatus = "testing",
            Visibility = "listed",
            CurrentMetadataVersionId = metadataId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
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
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.TitleReports.Add(new TitleReport
        {
            Id = reportId,
            TitleId = titleId,
            ReporterUserId = reporterUserId,
            Status = TitleReportStatuses.NeedsDeveloperResponse,
            Reason = "Report reason.",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await dbContext.SaveChangesAsync();
        return (titleId, reportId, developerUserId, reporterUserId);
    }

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        private readonly bool _useTestAuthentication;
        private readonly IReadOnlyList<Claim> _testClaims;
        private readonly string _inMemoryDatabaseName = $"board-enthusiasts-player-report-tests-{Guid.NewGuid():N}";

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
