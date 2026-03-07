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

namespace Board.ThirdPartyLibrary.Api.Tests;

/// <summary>
/// Endpoint tests for the Wave 4 media, releases, and artifacts API surface.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TitleWave4EndpointTests
{
    [Fact]
    public async Task GetCatalogTitleEndpoint_WithWave4Data_ReturnsMediaAndCurrentRelease()
    {
        var studioId = Guid.NewGuid();
        var titleId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();
        var releaseId = Guid.NewGuid();

        using var factory = new TestApiFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Studios.Add(new Studio
            {
                Id = studioId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.Titles.Add(new Title
            {
                Id = titleId,
                StudioId = studioId,
                Slug = "star-blasters",
                ContentKind = "game",
                LifecycleStatus = "testing",
                Visibility = "unlisted",
                CurrentMetadataVersionId = metadataId,
                CurrentReleaseId = releaseId,
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
            dbContext.TitleMediaAssets.AddRange(
                new TitleMediaAsset
                {
                    Id = Guid.NewGuid(),
                    TitleId = titleId,
                    MediaRole = "card",
                    SourceUrl = "https://cdn.example.com/card.png",
                    AltText = "Card art.",
                    MimeType = "image/png",
                    Width = 640,
                    Height = 360,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new TitleMediaAsset
                {
                    Id = Guid.NewGuid(),
                    TitleId = titleId,
                    MediaRole = "hero",
                    SourceUrl = "https://cdn.example.com/hero.png",
                    AltText = "Hero art.",
                    MimeType = "image/png",
                    Width = 1920,
                    Height = 1080,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            dbContext.TitleReleases.Add(new TitleRelease
            {
                Id = releaseId,
                TitleId = titleId,
                MetadataVersionId = metadataId,
                Version = "1.0.0",
                Status = "published",
                PublishedAtUtc = DateTime.UtcNow,
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

        Assert.Equal("https://cdn.example.com/card.png", title.GetProperty("cardImageUrl").GetString());
        Assert.Equal(2, title.GetProperty("mediaAssets").GetArrayLength());
        Assert.Equal("1.0.0", title.GetProperty("currentRelease").GetProperty("version").GetString());
    }

    [Fact]
    public async Task UpsertTitleMediaAssetEndpoint_WithEditorMembership_PersistsAsset()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "editor-123")]);

        Guid titleId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            titleId = await SeedManagedTitleAsync(dbContext, "editor-123");
        }

        using var client = factory.CreateClient();
        using var response = await client.PutAsJsonAsync(
            $"/developer/titles/{titleId}/media/card",
            new
            {
                sourceUrl = "https://cdn.example.com/card.png",
                altText = "Card art.",
                mimeType = "image/png",
                width = 640,
                height = 360
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("card", document.RootElement.GetProperty("mediaAsset").GetProperty("mediaRole").GetString());

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var mediaAsset = await verificationDbContext.TitleMediaAssets.SingleAsync(candidate => candidate.TitleId == titleId);
        Assert.Equal("https://cdn.example.com/card.png", mediaAsset.SourceUrl);
    }

    [Fact]
    public async Task UploadTitleMediaAssetEndpoint_WithEditorMembership_PersistsAsset()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "editor-123")]);

        Guid titleId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            titleId = await SeedManagedTitleAsync(dbContext, "editor-123");
        }

        using var client = factory.CreateClient();
        using var content = new MultipartFormDataContent();
        var mediaBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        using var mediaContent = new ByteArrayContent(mediaBytes);
        mediaContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(mediaContent, "media", "hero.png");
        content.Add(new StringContent("Hero art."), "altText");

        using var response = await client.PostAsync($"/developer/titles/{titleId}/media/hero/upload", content);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var mediaAsset = document.RootElement.GetProperty("mediaAsset");
        Assert.Equal("hero", mediaAsset.GetProperty("mediaRole").GetString());
        Assert.StartsWith("http://localhost/uploads/title-media/", mediaAsset.GetProperty("sourceUrl").GetString(), StringComparison.Ordinal);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var savedAsset = await verificationDbContext.TitleMediaAssets.SingleAsync(candidate => candidate.TitleId == titleId);
        Assert.StartsWith("http://localhost/uploads/title-media/", savedAsset.SourceUrl, StringComparison.Ordinal);
        Assert.Equal("image/png", savedAsset.MimeType);
    }

    [Fact]
    public async Task UploadTitleMediaAssetEndpoint_WithOversizedFile_ReturnsUnprocessableEntity()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "editor-123")]);

        Guid titleId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            titleId = await SeedManagedTitleAsync(dbContext, "editor-123");
        }

        using var client = factory.CreateClient();
        using var content = new MultipartFormDataContent();
        var mediaBytes = new byte[(25 * 1024 * 1024) + 1];
        using var mediaContent = new ByteArrayContent(mediaBytes);
        mediaContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(mediaContent, "media", "huge.png");

        using var response = await client.PostAsync($"/developer/titles/{titleId}/media/card/upload", content);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("media", out _));
    }

    [Fact]
    public async Task UploadTitleMediaAssetEndpoint_WithInvalidRole_ReturnsUnprocessableEntity()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "editor-123")]);

        Guid titleId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            titleId = await SeedManagedTitleAsync(dbContext, "editor-123");
        }

        using var client = factory.CreateClient();
        using var content = new MultipartFormDataContent();
        using var mediaContent = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        mediaContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(mediaContent, "media", "poster.png");

        using var response = await client.PostAsync($"/developer/titles/{titleId}/media/poster/upload", content);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("mediaRole", out _));
    }

    [Fact]
    public async Task UploadTitleMediaAssetEndpoint_WithoutMedia_ReturnsUnprocessableEntity()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "editor-123")]);

        Guid titleId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            titleId = await SeedManagedTitleAsync(dbContext, "editor-123");
        }

        using var client = factory.CreateClient();
        using var content = new MultipartFormDataContent
        {
            { new StringContent("Hero art."), "altText" }
        };

        using var response = await client.PostAsync($"/developer/titles/{titleId}/media/hero/upload", content);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("media", out _));
    }

    [Fact]
    public async Task UploadTitleMediaAssetEndpoint_WithUnsupportedFormat_ReturnsUnprocessableEntity()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "editor-123")]);

        Guid titleId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            titleId = await SeedManagedTitleAsync(dbContext, "editor-123");
        }

        using var client = factory.CreateClient();
        using var content = new MultipartFormDataContent();
        using var mediaContent = new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        mediaContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(mediaContent, "media", "hero.pdf");

        using var response = await client.PostAsync($"/developer/titles/{titleId}/media/hero/upload", content);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("media", out _));

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        Assert.False(await verificationDbContext.TitleMediaAssets.AnyAsync(candidate => candidate.TitleId == titleId));
    }

    [Fact]
    public async Task UploadTitleMediaAssetEndpoint_WithUnknownTitle_ReturnsNotFound()
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
        using var content = new MultipartFormDataContent();
        using var mediaContent = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        mediaContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(mediaContent, "media", "hero.png");

        using var response = await client.PostAsync($"/developer/titles/{Guid.NewGuid()}/media/hero/upload", content);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpsertTitleMediaAssetEndpoint_WithInvalidPayload_ReturnsUnprocessableEntity()
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
        using var response = await client.PutAsJsonAsync(
            $"/developer/titles/{Guid.NewGuid()}/media/unknown",
            new
            {
                sourceUrl = "not-a-url",
                width = 640
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("mediaRole", out _));
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("sourceUrl", out _));
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("dimensions", out _));
    }

    [Fact]
    public async Task DeleteTitleMediaAssetEndpoint_WithInvalidRole_ReturnsUnprocessableEntity()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "editor-123")]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            await SeedManagedTitleAsync(dbContext, "editor-123");
        }

        using var client = factory.CreateClient();
        using var response = await client.DeleteAsync($"/developer/titles/{Guid.NewGuid()}/media/poster");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("mediaRole", out _));
    }

    [Fact]
    public async Task CreateTitleReleaseEndpoint_WithEditorMembership_PersistsDraftRelease()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "editor-123")]);

        Guid titleId;
        Guid metadataId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            (titleId, metadataId) = await SeedManagedTitleWithMetadataAsync(dbContext, "editor-123");
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"/developer/titles/{titleId}/releases",
            new
            {
                version = "1.0.0",
                metadataRevisionNumber = 1
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var releaseId = Guid.Parse(document.RootElement.GetProperty("release").GetProperty("id").GetString()!);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var release = await verificationDbContext.TitleReleases.SingleAsync(candidate => candidate.Id == releaseId);
        Assert.Equal(metadataId, release.MetadataVersionId);
        Assert.Equal("draft", release.Status);
    }

    [Fact]
    public async Task CreateTitleReleaseEndpoint_WithInvalidPayload_ReturnsUnprocessableEntity()
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
            $"/developer/titles/{Guid.NewGuid()}/releases",
            new
            {
                version = "not-semver",
                metadataRevisionNumber = 0
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("version", out _));
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("metadataRevisionNumber", out _));
    }

    [Fact]
    public async Task CreateTitleReleaseEndpoint_WhenMetadataRevisionIsMissing_ReturnsNotFound()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "editor-123")]);

        Guid titleId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            titleId = await SeedManagedTitleAsync(dbContext, "editor-123");
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"/developer/titles/{titleId}/releases",
            new
            {
                version = "1.0.0",
                metadataRevisionNumber = 99
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PublishTitleReleaseEndpoint_WithoutArtifacts_ReturnsConflict()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "editor-123")]);

        Guid titleId;
        Guid releaseId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            (titleId, releaseId) = await SeedManagedDraftReleaseAsync(dbContext, "editor-123");
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsync($"/developer/titles/{titleId}/releases/{releaseId}/publish", null);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("title_release_publish_requires_artifact", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ActivateTitleReleaseEndpoint_WithDraftRelease_ReturnsConflict()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "editor-123")]);

        Guid titleId;
        Guid releaseId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            (titleId, releaseId) = await SeedManagedDraftReleaseAsync(dbContext, "editor-123");
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsync($"/developer/titles/{titleId}/releases/{releaseId}/activate", null);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("title_release_state_conflict", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateReleaseArtifactEndpoint_WithInvalidPayload_ReturnsUnprocessableEntity()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "editor-123")]);

        Guid titleId;
        Guid releaseId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            (titleId, releaseId) = await SeedManagedDraftReleaseAsync(dbContext, "editor-123");
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"/developer/titles/{titleId}/releases/{releaseId}/artifacts",
            new
            {
                artifactKind = "zip",
                packageName = "bad package",
                versionCode = 0,
                sha256 = "xyz",
                fileSizeBytes = -1
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("artifactKind", out _));
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("packageName", out _));
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("versionCode", out _));
    }

    [Fact]
    public async Task CreateReleaseArtifactEndpoint_WithDraftRelease_PersistsArtifact()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "editor-123")]);

        Guid titleId;
        Guid releaseId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            (titleId, releaseId) = await SeedManagedDraftReleaseAsync(dbContext, "editor-123");
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"/developer/titles/{titleId}/releases/{releaseId}/artifacts",
            new
            {
                artifactKind = "apk",
                packageName = "fun.board.starblasters",
                versionCode = 100L,
                sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                fileSizeBytes = 104857600L
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var artifact = await verificationDbContext.ReleaseArtifacts.SingleAsync(candidate => candidate.ReleaseId == releaseId);
        Assert.Equal("fun.board.starblasters", artifact.PackageName);
    }

    [Fact]
    public async Task UpdateReleaseArtifactEndpoint_WhenReleaseIsPublished_ReturnsConflict()
    {
        using var factory = new TestApiFactory(useTestAuthentication: true, testClaims: [new Claim("sub", "editor-123")]);

        Guid titleId;
        Guid releaseId;
        Guid artifactId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            (titleId, releaseId, artifactId) = await SeedManagedPublishedReleaseWithArtifactAsync(dbContext, "editor-123");
        }

        using var client = factory.CreateClient();
        using var response = await client.PutAsJsonAsync(
            $"/developer/titles/{titleId}/releases/{releaseId}/artifacts/{artifactId}",
            new
            {
                artifactKind = "apk",
                packageName = "fun.board.starblasters",
                versionCode = 101L,
                sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                fileSizeBytes = 2048L
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("title_release_state_conflict", document.RootElement.GetProperty("code").GetString());
    }

    private static async Task<Guid> SeedManagedTitleAsync(BoardLibraryDbContext dbContext, string subject)
    {
        var (titleId, _) = await SeedManagedTitleWithMetadataAsync(dbContext, subject);
        return titleId;
    }

    private static async Task<(Guid TitleId, Guid MetadataId)> SeedManagedTitleWithMetadataAsync(BoardLibraryDbContext dbContext, string subject)
    {
        var studioId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var titleId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();

        dbContext.Users.Add(new AppUser
        {
            Id = userId,
            KeycloakSubject = subject,
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
        dbContext.Titles.Add(new Title
        {
            Id = titleId,
            StudioId = studioId,
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
        return (titleId, metadataId);
    }

    private static async Task<(Guid TitleId, Guid ReleaseId)> SeedManagedDraftReleaseAsync(BoardLibraryDbContext dbContext, string subject)
    {
        var (titleId, metadataId) = await SeedManagedTitleWithMetadataAsync(dbContext, subject);
        var releaseId = Guid.NewGuid();
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
        return (titleId, releaseId);
    }

    private static async Task<(Guid TitleId, Guid ReleaseId, Guid ArtifactId)> SeedManagedPublishedReleaseWithArtifactAsync(BoardLibraryDbContext dbContext, string subject)
    {
        var (titleId, metadataId) = await SeedManagedTitleWithMetadataAsync(dbContext, subject);
        var releaseId = Guid.NewGuid();
        var artifactId = Guid.NewGuid();
        dbContext.TitleReleases.Add(new TitleRelease
        {
            Id = releaseId,
            TitleId = titleId,
            MetadataVersionId = metadataId,
            Version = "1.0.0",
            Status = "published",
            PublishedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        dbContext.ReleaseArtifacts.Add(new ReleaseArtifact
        {
            Id = artifactId,
            ReleaseId = releaseId,
            ArtifactKind = "apk",
            PackageName = "fun.board.starblasters",
            VersionCode = 100,
            Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            FileSizeBytes = 2048,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        return (titleId, releaseId, artifactId);
    }

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        private readonly bool _useTestAuthentication;
        private readonly IReadOnlyList<Claim> _testClaims;
        private readonly string _inMemoryDatabaseName = $"board-enthusiasts-wave4-tests-{Guid.NewGuid():N}";

        public TestApiFactory(bool useTestAuthentication = false, IEnumerable<Claim>? testClaims = null)
        {
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
