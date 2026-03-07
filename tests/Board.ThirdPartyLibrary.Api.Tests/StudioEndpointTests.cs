using System.Net;
using System.Net.Http.Headers;
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
/// Endpoint tests for the Wave 2 studio API surface.
/// </summary>
[Trait("Category", "Unit")]
public sealed class StudioEndpointTests
{
    /// <summary>
    /// Verifies public studio listing returns persisted studios.
    /// </summary>
    [Fact]
    public async Task ListStudiosEndpoint_ReturnsPublicStudios()
    {
        using var factory = new TestApiFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Studios.AddRange(
                new Studio
                {
                    Id = Guid.NewGuid(),
                    Slug = "stellar-forge",
                    DisplayName = "Stellar Forge",
                    Description = "Family co-op studio.",
                    LogoUrl = "https://cdn.example.com/orgs/stellar-forge.png",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new Studio
                {
                    Id = Guid.NewGuid(),
                    Slug = "tabletop-sparks",
                    DisplayName = "Tabletop Sparks",
                    Description = "Party game makers.",
                    LogoUrl = null,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/studios");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var studios = document.RootElement.GetProperty("studios").EnumerateArray().ToList();

        Assert.Equal(2, studios.Count);
        Assert.Contains(studios, studio => studio.GetProperty("slug").GetString() == "stellar-forge");
        Assert.Contains(studios, studio => studio.GetProperty("slug").GetString() == "tabletop-sparks");
    }

    /// <summary>
    /// Verifies authenticated developers can list only studios they manage.
    /// </summary>
    [Fact]
    public async Task ListManagedStudiosEndpoint_ReturnsCallerStudios()
    {
        var managedStudioId = Guid.NewGuid();
        var unmanagedStudioId = Guid.NewGuid();
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
            dbContext.Studios.AddRange(
                new Studio
                {
                    Id = managedStudioId,
                    Slug = "stellar-forge",
                    DisplayName = "Stellar Forge",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new Studio
                {
                    Id = unmanagedStudioId,
                    Slug = "tabletop-sparks",
                    DisplayName = "Tabletop Sparks",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            dbContext.StudioMemberships.Add(new StudioMembership
            {
                StudioId = managedStudioId,
                UserId = userId,
                Role = "editor",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/developer/studios");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var studios = document.RootElement.GetProperty("studios").EnumerateArray().ToList();

        Assert.Single(studios);
        Assert.Equal(managedStudioId.ToString(), studios[0].GetProperty("id").GetString());
        Assert.Equal("editor", studios[0].GetProperty("role").GetString());
    }

    /// <summary>
    /// Verifies public studio details are resolved by slug.
    /// </summary>
    [Fact]
    public async Task GetStudioBySlugEndpoint_WhenStudioExists_ReturnsPublicDetails()
    {
        var studioId = Guid.NewGuid();

        using var factory = new TestApiFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Studios.Add(new Studio
            {
                Id = studioId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                Description = "Family co-op studio.",
                LogoUrl = "https://cdn.example.com/orgs/stellar-forge.png",
                BannerUrl = "https://cdn.example.com/orgs/stellar-forge-banner.png",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.StudioLinks.Add(new StudioLink
            {
                Id = Guid.NewGuid(),
                StudioId = studioId,
                Label = "Discord",
                Url = "https://discord.gg/stellarforge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/studios/stellar-forge");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var studio = document.RootElement.GetProperty("studio");

        Assert.Equal(studioId.ToString(), studio.GetProperty("id").GetString());
        Assert.Equal("Stellar Forge", studio.GetProperty("displayName").GetString());
        Assert.Equal("stellar-forge", studio.GetProperty("slug").GetString());
        Assert.Equal("https://cdn.example.com/orgs/stellar-forge-banner.png", studio.GetProperty("bannerUrl").GetString());
        Assert.Single(studio.GetProperty("links").EnumerateArray());
    }

    /// <summary>
    /// Verifies missing public studio details return not found.
    /// </summary>
    [Fact]
    public async Task GetStudioBySlugEndpoint_WhenStudioIsMissing_ReturnsNotFound()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/studios/missing-studio");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies creating a studio requires authentication.
    /// </summary>
    [Fact]
    public async Task CreateStudioEndpoint_WithoutBearerToken_ReturnsUnauthorized()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/studios",
            new
            {
                slug = "stellar-forge",
                displayName = "Stellar Forge"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Verifies only developer-capable platform roles can create studios.
    /// </summary>
    [Fact]
    public async Task CreateStudioEndpoint_WithoutDeveloperRole_ReturnsForbidden()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim("name", "Player One"),
                new Claim(ClaimTypes.Role, "player")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/studios",
            new
            {
                slug = "stellar-forge",
                displayName = "Stellar Forge"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Verifies studio creation succeeds when Keycloak already shows developer role before token refresh.
    /// </summary>
    [Fact]
    public async Task CreateStudioEndpoint_WithKeycloakDeveloperRole_AllowsCreate()
    {
        var roleClient = new StubKeycloakUserRoleClient(
            KeycloakUserRoleCheckResult.Success(isAssigned: true));

        using var factory = new TestApiFactory(
            configureServices: services =>
            {
                services.RemoveAll<IKeycloakUserRoleClient>();
                services.AddSingleton<IKeycloakUserRoleClient>(roleClient);
            },
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim("name", "Player One"),
                new Claim("email", "player@boardtpl.local"),
                new Claim(ClaimTypes.Role, "player")
            ]);

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/studios",
            new
            {
                slug = "stellar-forge",
                displayName = "Stellar Forge"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, roleClient.RoleCheckCallCount);
    }

    /// <summary>
    /// Verifies a developer can create a studio and becomes the initial owner.
    /// </summary>
    [Fact]
    public async Task CreateStudioEndpoint_WithDeveloperRole_PersistsStudioAndOwnerMembership()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim("name", "Local Developer"),
                new Claim("email", "dev@boardtpl.local"),
                new Claim(ClaimTypes.Role, "developer")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/studios",
            new
            {
                slug = "stellar-forge",
                displayName = "Stellar Forge",
                description = "Family co-op studio.",
                logoUrl = "https://cdn.example.com/orgs/stellar-forge.png",
                bannerUrl = "https://cdn.example.com/orgs/stellar-forge-banner.png"
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var studio = document.RootElement.GetProperty("studio");
        var studioId = Guid.Parse(studio.GetProperty("id").GetString()!);

        Assert.Equal("stellar-forge", studio.GetProperty("slug").GetString());
        Assert.Equal("Stellar Forge", studio.GetProperty("displayName").GetString());
        Assert.Equal("https://cdn.example.com/orgs/stellar-forge-banner.png", studio.GetProperty("bannerUrl").GetString());
        Assert.Empty(studio.GetProperty("links").EnumerateArray());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var persistedMembership = await dbContext.StudioMemberships
            .Include(candidate => candidate.User)
            .SingleAsync(candidate => candidate.StudioId == studioId);
        var persistedStudio = await dbContext.Studios.SingleAsync(candidate => candidate.Id == studioId);

        Assert.Equal("owner", persistedMembership.Role);
        Assert.Equal("user-123", persistedMembership.User.KeycloakSubject);
        Assert.Equal("https://cdn.example.com/orgs/stellar-forge-banner.png", persistedStudio.BannerUrl);
    }

    /// <summary>
    /// Verifies invalid studio payloads are rejected.
    /// </summary>
    [Fact]
    public async Task CreateStudioEndpoint_WithInvalidPayload_ReturnsUnprocessableEntity()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim(ClaimTypes.Role, "developer")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/studios",
            new
            {
                slug = "Invalid Slug",
                displayName = "",
                logoUrl = "not-a-valid-uri"
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode)StatusCodes.Status422UnprocessableEntity, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var errors = document.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("slug", out _));
        Assert.True(errors.TryGetProperty("displayName", out _));
        Assert.True(errors.TryGetProperty("logoUrl", out _));
    }


    /// <summary>
    /// Verifies studio owners can update studio details.
    /// </summary>
    [Fact]
    public async Task UpdateStudioEndpoint_WithOwnerMembership_UpdatesStudio()
    {
        var studioId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim("name", "Local Developer"),
                new Claim(ClaimTypes.Role, "developer")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.Add(new AppUser
            {
                Id = userId,
                KeycloakSubject = "user-123",
                DisplayName = "Local Developer",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.Studios.Add(new Studio
            {
                Id = studioId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                Description = "Original description.",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
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

        using var client = factory.CreateClient();
        using var response = await client.PutAsJsonAsync(
            $"/developer/studios/{studioId}",
            new
            {
                slug = "stellar-forge-studio",
                displayName = "Stellar Forge Studio",
                description = "Updated description.",
                logoUrl = "https://cdn.example.com/orgs/stellar-forge-studio.png",
                bannerUrl = "https://cdn.example.com/orgs/stellar-forge-studio-banner.png"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var studio = await verificationDbContext.Studios.SingleAsync(candidate => candidate.Id == studioId);

        Assert.Equal("stellar-forge-studio", studio.Slug);
        Assert.Equal("Stellar Forge Studio", studio.DisplayName);
        Assert.Equal("Updated description.", studio.Description);
        Assert.Equal("https://cdn.example.com/orgs/stellar-forge-studio-banner.png", studio.BannerUrl);
    }

    /// <summary>
    /// Verifies studio owners can create and list public studio links.
    /// </summary>
    [Fact]
    public async Task StudioLinksEndpoints_WithOwnerMembership_CreateAndListLinks()
    {
        var studioId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "owner-123"),
                new Claim(ClaimTypes.Role, "developer")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.Add(new AppUser
            {
                Id = userId,
                KeycloakSubject = "owner-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.Studios.Add(new Studio
            {
                Id = studioId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
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

        using var client = factory.CreateClient();
        using var createResponse = await client.PostAsJsonAsync(
            $"/developer/studios/{studioId}/links",
            new
            {
                label = "Discord",
                url = "https://discord.gg/stellarforge"
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var listResponse = await client.GetAsync($"/developer/studios/{studioId}/links");
        var payload = await listResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var links = document.RootElement.GetProperty("links").EnumerateArray().ToList();
        Assert.Single(links);
        Assert.Equal("Discord", links[0].GetProperty("label").GetString());

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        Assert.True(await verificationDbContext.StudioLinks.AnyAsync(candidate => candidate.StudioId == studioId && candidate.Label == "Discord"));
    }

    /// <summary>
    /// Verifies studio owners can update and delete public studio links.
    /// </summary>
    [Fact]
    public async Task StudioLinksEndpoints_WithOwnerMembership_UpdateAndDeleteLinks()
    {
        var studioId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var linkId = Guid.NewGuid();

        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "owner-123"),
                new Claim(ClaimTypes.Role, "developer")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.Add(new AppUser
            {
                Id = userId,
                KeycloakSubject = "owner-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.Studios.Add(new Studio
            {
                Id = studioId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.StudioMemberships.Add(new StudioMembership
            {
                StudioId = studioId,
                UserId = userId,
                Role = "owner",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.StudioLinks.Add(new StudioLink
            {
                Id = linkId,
                StudioId = studioId,
                Label = "Discord",
                Url = "https://discord.gg/stellarforge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var updateResponse = await client.PutAsJsonAsync(
            $"/developer/studios/{studioId}/links/{linkId}",
            new
            {
                label = "Community Discord",
                url = "https://discord.gg/stellarforgehq"
            });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var deleteResponse = await client.DeleteAsync($"/developer/studios/{studioId}/links/{linkId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        Assert.False(await verificationDbContext.StudioLinks.AnyAsync(candidate => candidate.Id == linkId));
    }

    /// <summary>
    /// Verifies studio owners can upload logo media.
    /// </summary>
    [Fact]
    public async Task UploadStudioLogoEndpoint_WithOwnerMembership_PersistsMedia()
    {
        var studioId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "owner-123"),
                new Claim(ClaimTypes.Role, "developer")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.Add(new AppUser
            {
                Id = userId,
                KeycloakSubject = "owner-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.Studios.Add(new Studio
            {
                Id = studioId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
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

        using var client = factory.CreateClient();
        using var content = new MultipartFormDataContent();
        using var mediaContent = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
        mediaContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(mediaContent, "media", "logo.png");

        using var response = await client.PostAsync($"/developer/studios/{studioId}/logo-upload", content);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.StartsWith("http://localhost/uploads/studio-media/", document.RootElement.GetProperty("studio").GetProperty("logoUrl").GetString(), StringComparison.Ordinal);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var studio = await verificationDbContext.Studios.SingleAsync(candidate => candidate.Id == studioId);
        Assert.StartsWith("http://localhost/uploads/studio-media/", studio.LogoUrl, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies studio updates reject invalid payloads.
    /// </summary>
    [Fact]
    public async Task UpdateStudioEndpoint_WithInvalidPayload_ReturnsUnprocessableEntity()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "owner-123"),
                new Claim(ClaimTypes.Role, "developer")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            $"/developer/studios/{Guid.NewGuid()}",
            new
            {
                slug = "Invalid Slug",
                displayName = "",
                logoUrl = "notaurl"
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode)StatusCodes.Status422UnprocessableEntity, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var errors = document.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("slug", out _));
        Assert.True(errors.TryGetProperty("displayName", out _));
        Assert.True(errors.TryGetProperty("logoUrl", out _));
    }

    /// <summary>
    /// Verifies only owners or admins can update studio details.
    /// </summary>
    [Fact]
    public async Task UpdateStudioEndpoint_WithoutManagementMembership_ReturnsForbidden()
    {
        var studioId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "editor-123"),
                new Claim(ClaimTypes.Role, "developer")
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
            dbContext.Studios.Add(new Studio
            {
                Id = studioId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.StudioMemberships.Add(new StudioMembership
            {
                StudioId = studioId,
                UserId = userId,
                Role = "editor",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.PutAsJsonAsync(
            $"/developer/studios/{studioId}",
            new
            {
                slug = "stellar-forge-updated",
                displayName = "Stellar Forge Updated"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Verifies studio updates return not found for missing studios.
    /// </summary>
    [Fact]
    public async Task UpdateStudioEndpoint_WhenStudioIsMissing_ReturnsNotFound()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "owner-123"),
                new Claim(ClaimTypes.Role, "developer")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.Add(new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "owner-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.PutAsJsonAsync(
            $"/developer/studios/{Guid.NewGuid()}",
            new
            {
                slug = "stellar-forge-updated",
                displayName = "Stellar Forge Updated"
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }


    /// <summary>
    /// Verifies only studio owners or admins can view membership listings.
    /// </summary>
    [Fact]
    public async Task ListMembershipsEndpoint_WithoutManagementMembership_ReturnsForbidden()
    {
        var studioId = Guid.NewGuid();

        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim(ClaimTypes.Role, "developer")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.Add(new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "user-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.Studios.Add(new Studio
            {
                Id = studioId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/developer/studios/{studioId}/memberships");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Verifies owners can list studio memberships.
    /// </summary>
    [Fact]
    public async Task ListMembershipsEndpoint_WithOwnerMembership_ReturnsMembers()
    {
        var studioId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var editorUserId = Guid.NewGuid();

        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "owner-123"),
                new Claim("name", "Owner User"),
                new Claim(ClaimTypes.Role, "developer")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.AddRange(
                new AppUser
                {
                    Id = ownerUserId,
                    KeycloakSubject = "owner-123",
                    DisplayName = "Owner User",
                    Email = "owner@boardtpl.local",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new AppUser
                {
                    Id = editorUserId,
                    KeycloakSubject = "editor-456",
                    DisplayName = "Editor User",
                    Email = "editor@boardtpl.local",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            dbContext.Studios.Add(new Studio
            {
                Id = studioId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.StudioMemberships.AddRange(
                new StudioMembership
                {
                    StudioId = studioId,
                    UserId = ownerUserId,
                    Role = "owner",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new StudioMembership
                {
                    StudioId = studioId,
                    UserId = editorUserId,
                    Role = "editor",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/developer/studios/{studioId}/memberships");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var memberships = document.RootElement.GetProperty("memberships").EnumerateArray().ToList();

        Assert.Equal(2, memberships.Count);
        Assert.Contains(memberships, membership => membership.GetProperty("keycloakSubject").GetString() == "editor-456");
    }

    /// <summary>
    /// Verifies membership listing returns not found for missing studios.
    /// </summary>
    [Fact]
    public async Task ListMembershipsEndpoint_WhenStudioIsMissing_ReturnsNotFound()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "owner-123"),
                new Claim(ClaimTypes.Role, "developer")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.Add(new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "owner-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/developer/studios/{Guid.NewGuid()}/memberships");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies owners can add or update memberships by target Keycloak subject.
    /// </summary>
    [Fact]
    public async Task UpsertMembershipEndpoint_WithOwnerMembership_PersistsMembership()
    {
        var studioId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "owner-123"),
                new Claim(ClaimTypes.Role, "developer")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.AddRange(
                new AppUser
                {
                    Id = ownerUserId,
                    KeycloakSubject = "owner-123",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new AppUser
                {
                    Id = targetUserId,
                    KeycloakSubject = "editor-456",
                    DisplayName = "Editor User",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            dbContext.Studios.Add(new Studio
            {
                Id = studioId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.StudioMemberships.Add(new StudioMembership
            {
                StudioId = studioId,
                UserId = ownerUserId,
                Role = "owner",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.PutAsJsonAsync(
            $"/developer/studios/{studioId}/memberships/editor-456",
            new
            {
                role = "editor"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var membership = await verificationDbContext.StudioMemberships
            .Include(candidate => candidate.User)
            .SingleAsync(candidate => candidate.StudioId == studioId && candidate.User.KeycloakSubject == "editor-456");

        Assert.Equal("editor", membership.Role);
    }

    /// <summary>
    /// Verifies invalid membership roles are rejected before the service executes.
    /// </summary>
    [Fact]
    public async Task UpsertMembershipEndpoint_WithInvalidRole_ReturnsUnprocessableEntity()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "owner-123"),
                new Claim(ClaimTypes.Role, "developer")
            ]);
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            $"/developer/studios/{Guid.NewGuid()}/memberships/editor-456",
            new
            {
                role = "viewer"
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode)StatusCodes.Status422UnprocessableEntity, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("role", out _));
    }

    /// <summary>
    /// Verifies membership updates return a problem payload when the target user is missing locally.
    /// </summary>
    [Fact]
    public async Task UpsertMembershipEndpoint_WhenTargetUserIsMissing_ReturnsNotFoundProblem()
    {
        var studioId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();

        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "owner-123"),
                new Claim(ClaimTypes.Role, "developer")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.Add(new AppUser
            {
                Id = ownerUserId,
                KeycloakSubject = "owner-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.Studios.Add(new Studio
            {
                Id = studioId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.StudioMemberships.Add(new StudioMembership
            {
                StudioId = studioId,
                UserId = ownerUserId,
                Role = "owner",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.PutAsJsonAsync(
            $"/developer/studios/{studioId}/memberships/missing-user",
            new
            {
                role = "editor"
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("studio_member_target_not_found", document.RootElement.GetProperty("code").GetString());
    }

    /// <summary>
    /// Verifies non-managers cannot change memberships.
    /// </summary>
    [Fact]
    public async Task UpsertMembershipEndpoint_WithoutManagementMembership_ReturnsForbidden()
    {
        var studioId = Guid.NewGuid();
        var editorUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "editor-123"),
                new Claim(ClaimTypes.Role, "developer")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.AddRange(
                new AppUser
                {
                    Id = editorUserId,
                    KeycloakSubject = "editor-123",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new AppUser
                {
                    Id = targetUserId,
                    KeycloakSubject = "owner-456",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            dbContext.Studios.Add(new Studio
            {
                Id = studioId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.StudioMemberships.Add(new StudioMembership
            {
                StudioId = studioId,
                UserId = editorUserId,
                Role = "editor",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.PutAsJsonAsync(
            $"/developer/studios/{studioId}/memberships/owner-456",
            new
            {
                role = "admin"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Verifies the last owner cannot be removed from a studio.
    /// </summary>
    [Fact]
    public async Task DeleteMembershipEndpoint_WhenTargetIsLastOwner_ReturnsConflict()
    {
        var studioId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();

        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "owner-123"),
                new Claim(ClaimTypes.Role, "developer")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.Add(new AppUser
            {
                Id = ownerUserId,
                KeycloakSubject = "owner-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.Studios.Add(new Studio
            {
                Id = studioId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.StudioMemberships.Add(new StudioMembership
            {
                StudioId = studioId,
                UserId = ownerUserId,
                Role = "owner",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.DeleteAsync($"/developer/studios/{studioId}/memberships/owner-123");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>
    /// Verifies membership deletion returns not found when the target membership does not exist.
    /// </summary>
    [Fact]
    public async Task DeleteMembershipEndpoint_WhenMembershipIsMissing_ReturnsNotFound()
    {
        var studioId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();

        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "owner-123"),
                new Claim(ClaimTypes.Role, "developer")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.Add(new AppUser
            {
                Id = ownerUserId,
                KeycloakSubject = "owner-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.Studios.Add(new Studio
            {
                Id = studioId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.StudioMemberships.Add(new StudioMembership
            {
                StudioId = studioId,
                UserId = ownerUserId,
                Role = "owner",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.DeleteAsync($"/developer/studios/{studioId}/memberships/missing-user");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies non-managers cannot delete memberships.
    /// </summary>
    [Fact]
    public async Task DeleteMembershipEndpoint_WithoutManagementMembership_ReturnsForbidden()
    {
        var studioId = Guid.NewGuid();
        var editorUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();

        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "editor-123"),
                new Claim(ClaimTypes.Role, "developer")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.AddRange(
                new AppUser
                {
                    Id = editorUserId,
                    KeycloakSubject = "editor-123",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new AppUser
                {
                    Id = ownerUserId,
                    KeycloakSubject = "owner-456",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            dbContext.Studios.Add(new Studio
            {
                Id = studioId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.StudioMemberships.AddRange(
                new StudioMembership
                {
                    StudioId = studioId,
                    UserId = editorUserId,
                    Role = "editor",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new StudioMembership
                {
                    StudioId = studioId,
                    UserId = ownerUserId,
                    Role = "owner",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.DeleteAsync($"/developer/studios/{studioId}/memberships/owner-456");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Verifies owners can hard delete studios.
    /// </summary>
    [Fact]
    public async Task DeleteStudioEndpoint_WithOwnerMembership_RemovesStudio()
    {
        var studioId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();

        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "owner-123"),
                new Claim(ClaimTypes.Role, "developer")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.Add(new AppUser
            {
                Id = ownerUserId,
                KeycloakSubject = "owner-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.Studios.Add(new Studio
            {
                Id = studioId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.StudioMemberships.Add(new StudioMembership
            {
                StudioId = studioId,
                UserId = ownerUserId,
                Role = "owner",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.DeleteAsync($"/developer/studios/{studioId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();

        Assert.False(await verificationDbContext.Studios.AnyAsync(candidate => candidate.Id == studioId));
        Assert.False(await verificationDbContext.StudioMemberships.AnyAsync(candidate => candidate.StudioId == studioId));
    }

    /// <summary>
    /// Verifies only owners can delete studios.
    /// </summary>
    [Fact]
    public async Task DeleteStudioEndpoint_WithoutOwnerMembership_ReturnsForbidden()
    {
        var studioId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();

        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "admin-123"),
                new Claim(ClaimTypes.Role, "developer")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.Add(new AppUser
            {
                Id = adminUserId,
                KeycloakSubject = "admin-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.Studios.Add(new Studio
            {
                Id = studioId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.StudioMemberships.Add(new StudioMembership
            {
                StudioId = studioId,
                UserId = adminUserId,
                Role = "admin",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.DeleteAsync($"/developer/studios/{studioId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Verifies delete requests return not found for missing studios.
    /// </summary>
    [Fact]
    public async Task DeleteStudioEndpoint_WhenStudioIsMissing_ReturnsNotFound()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "owner-123"),
                new Claim(ClaimTypes.Role, "developer")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Users.Add(new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "owner-123",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.DeleteAsync($"/developer/studios/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        private readonly Action<IServiceCollection>? _configureServices;
        private readonly Action<IConfigurationBuilder>? _configureConfiguration;
        private readonly bool _useTestAuthentication;
        private readonly IReadOnlyList<Claim> _testClaims;
        private readonly string _inMemoryDatabaseName = $"board-third-party-lib-org-tests-{Guid.NewGuid():N}";

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

    private sealed class StubKeycloakUserRoleClient(KeycloakUserRoleCheckResult roleCheckResult) : IKeycloakUserRoleClient
    {
        public int RoleCheckCallCount { get; private set; }

        public Task<KeycloakUserRoleMutationResult> EnsureRealmRoleAssignedAsync(
            string userSubject,
            string roleName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(KeycloakUserRoleMutationResult.Success(alreadyInRequestedState: false));

        public Task<KeycloakUserRoleMutationResult> EnsureRealmRoleRemovedAsync(
            string userSubject,
            string roleName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(KeycloakUserRoleMutationResult.Success(alreadyInRequestedState: false));

        public Task<KeycloakUserRoleCheckResult> IsRealmRoleAssignedAsync(
            string userSubject,
            string roleName,
            CancellationToken cancellationToken = default)
        {
            RoleCheckCallCount++;
            return Task.FromResult(roleCheckResult);
        }
    }
}

