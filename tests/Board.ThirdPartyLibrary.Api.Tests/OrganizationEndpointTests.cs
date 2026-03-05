using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
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
/// Endpoint tests for the Wave 2 organization API surface.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OrganizationEndpointTests
{
    /// <summary>
    /// Verifies public organization listing returns persisted organizations.
    /// </summary>
    [Fact]
    public async Task ListOrganizationsEndpoint_ReturnsPublicOrganizations()
    {
        using var factory = new TestApiFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Organizations.AddRange(
                new Organization
                {
                    Id = Guid.NewGuid(),
                    Slug = "stellar-forge",
                    DisplayName = "Stellar Forge",
                    Description = "Family co-op studio.",
                    LogoUrl = "https://cdn.example.com/orgs/stellar-forge.png",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new Organization
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
        using var response = await client.GetAsync("/organizations");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var organizations = document.RootElement.GetProperty("organizations").EnumerateArray().ToList();

        Assert.Equal(2, organizations.Count);
        Assert.Contains(organizations, organization => organization.GetProperty("slug").GetString() == "stellar-forge");
        Assert.Contains(organizations, organization => organization.GetProperty("slug").GetString() == "tabletop-sparks");
    }

    /// <summary>
    /// Verifies authenticated developers can list only organizations they manage.
    /// </summary>
    [Fact]
    public async Task ListManagedOrganizationsEndpoint_ReturnsCallerOrganizations()
    {
        var managedOrganizationId = Guid.NewGuid();
        var unmanagedOrganizationId = Guid.NewGuid();
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
            dbContext.Organizations.AddRange(
                new Organization
                {
                    Id = managedOrganizationId,
                    Slug = "stellar-forge",
                    DisplayName = "Stellar Forge",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new Organization
                {
                    Id = unmanagedOrganizationId,
                    Slug = "tabletop-sparks",
                    DisplayName = "Tabletop Sparks",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            dbContext.OrganizationMemberships.Add(new OrganizationMembership
            {
                OrganizationId = managedOrganizationId,
                UserId = userId,
                Role = "editor",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/developer/organizations");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var organizations = document.RootElement.GetProperty("organizations").EnumerateArray().ToList();

        Assert.Single(organizations);
        Assert.Equal(managedOrganizationId.ToString(), organizations[0].GetProperty("id").GetString());
        Assert.Equal("editor", organizations[0].GetProperty("role").GetString());
    }

    /// <summary>
    /// Verifies public organization details are resolved by slug.
    /// </summary>
    [Fact]
    public async Task GetOrganizationBySlugEndpoint_WhenOrganizationExists_ReturnsPublicDetails()
    {
        var organizationId = Guid.NewGuid();

        using var factory = new TestApiFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            dbContext.Organizations.Add(new Organization
            {
                Id = organizationId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                Description = "Family co-op studio.",
                LogoUrl = "https://cdn.example.com/orgs/stellar-forge.png",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/organizations/stellar-forge");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var organization = document.RootElement.GetProperty("organization");

        Assert.Equal(organizationId.ToString(), organization.GetProperty("id").GetString());
        Assert.Equal("Stellar Forge", organization.GetProperty("displayName").GetString());
        Assert.Equal("stellar-forge", organization.GetProperty("slug").GetString());
    }

    /// <summary>
    /// Verifies missing public organization details return not found.
    /// </summary>
    [Fact]
    public async Task GetOrganizationBySlugEndpoint_WhenOrganizationIsMissing_ReturnsNotFound()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/organizations/missing-studio");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies creating an organization requires authentication.
    /// </summary>
    [Fact]
    public async Task CreateOrganizationEndpoint_WithoutBearerToken_ReturnsUnauthorized()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/organizations",
            new
            {
                slug = "stellar-forge",
                displayName = "Stellar Forge"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Verifies only developer-capable platform roles can create organizations.
    /// </summary>
    [Fact]
    public async Task CreateOrganizationEndpoint_WithoutDeveloperRole_ReturnsForbidden()
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
            "/organizations",
            new
            {
                slug = "stellar-forge",
                displayName = "Stellar Forge"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Verifies approved developer enrollment also unlocks organization creation before a refreshed token.
    /// </summary>
    [Fact]
    public async Task CreateOrganizationEndpoint_WithApprovedEnrollment_AllowsCreate()
    {
        using var factory = new TestApiFactory(
            useTestAuthentication: true,
            testClaims:
            [
                new Claim("sub", "user-123"),
                new Claim("name", "Player One"),
                new Claim("email", "player@boardtpl.local"),
                new Claim(ClaimTypes.Role, "player")
            ]);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                KeycloakSubject = "user-123",
                DisplayName = "Player One",
                Email = "player@boardtpl.local",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            var thread = new ConversationThread
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            };

            dbContext.Users.Add(user);
            dbContext.ConversationThreads.Add(thread);
            dbContext.DeveloperEnrollmentRequests.Add(new DeveloperEnrollmentRequest
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Status = DeveloperEnrollmentStatuses.Approved,
                ConversationThreadId = thread.Id,
                ConversationThread = thread,
                RequestedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                ReviewedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/organizations",
            new
            {
                slug = "stellar-forge",
                displayName = "Stellar Forge"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>
    /// Verifies a developer can create an organization and becomes the initial owner.
    /// </summary>
    [Fact]
    public async Task CreateOrganizationEndpoint_WithDeveloperRole_PersistsOrganizationAndOwnerMembership()
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
            "/organizations",
            new
            {
                slug = "stellar-forge",
                displayName = "Stellar Forge",
                description = "Family co-op studio.",
                logoUrl = "https://cdn.example.com/orgs/stellar-forge.png"
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var organization = document.RootElement.GetProperty("organization");
        var organizationId = Guid.Parse(organization.GetProperty("id").GetString()!);

        Assert.Equal("stellar-forge", organization.GetProperty("slug").GetString());
        Assert.Equal("Stellar Forge", organization.GetProperty("displayName").GetString());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var persistedMembership = await dbContext.OrganizationMemberships
            .Include(candidate => candidate.User)
            .SingleAsync(candidate => candidate.OrganizationId == organizationId);

        Assert.Equal("owner", persistedMembership.Role);
        Assert.Equal("user-123", persistedMembership.User.KeycloakSubject);
    }

    /// <summary>
    /// Verifies invalid organization payloads are rejected.
    /// </summary>
    [Fact]
    public async Task CreateOrganizationEndpoint_WithInvalidPayload_ReturnsUnprocessableEntity()
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
            "/organizations",
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
    /// Verifies organization owners can update organization details.
    /// </summary>
    [Fact]
    public async Task UpdateOrganizationEndpoint_WithOwnerMembership_UpdatesOrganization()
    {
        var organizationId = Guid.NewGuid();
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
            dbContext.Organizations.Add(new Organization
            {
                Id = organizationId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                Description = "Original description.",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.OrganizationMemberships.Add(new OrganizationMembership
            {
                OrganizationId = organizationId,
                UserId = userId,
                Role = "owner",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.PutAsJsonAsync(
            $"/developer/organizations/{organizationId}",
            new
            {
                slug = "stellar-forge-studio",
                displayName = "Stellar Forge Studio",
                description = "Updated description.",
                logoUrl = "https://cdn.example.com/orgs/stellar-forge-studio.png"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var organization = await verificationDbContext.Organizations.SingleAsync(candidate => candidate.Id == organizationId);

        Assert.Equal("stellar-forge-studio", organization.Slug);
        Assert.Equal("Stellar Forge Studio", organization.DisplayName);
        Assert.Equal("Updated description.", organization.Description);
    }

    /// <summary>
    /// Verifies organization updates reject invalid payloads.
    /// </summary>
    [Fact]
    public async Task UpdateOrganizationEndpoint_WithInvalidPayload_ReturnsUnprocessableEntity()
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
            $"/developer/organizations/{Guid.NewGuid()}",
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
    /// Verifies only owners or admins can update organization details.
    /// </summary>
    [Fact]
    public async Task UpdateOrganizationEndpoint_WithoutManagementMembership_ReturnsForbidden()
    {
        var organizationId = Guid.NewGuid();
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
        using var response = await client.PutAsJsonAsync(
            $"/developer/organizations/{organizationId}",
            new
            {
                slug = "stellar-forge-updated",
                displayName = "Stellar Forge Updated"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Verifies organization updates return not found for missing organizations.
    /// </summary>
    [Fact]
    public async Task UpdateOrganizationEndpoint_WhenOrganizationIsMissing_ReturnsNotFound()
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
            $"/developer/organizations/{Guid.NewGuid()}",
            new
            {
                slug = "stellar-forge-updated",
                displayName = "Stellar Forge Updated"
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }


    /// <summary>
    /// Verifies only organization owners or admins can view membership listings.
    /// </summary>
    [Fact]
    public async Task ListMembershipsEndpoint_WithoutManagementMembership_ReturnsForbidden()
    {
        var organizationId = Guid.NewGuid();

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
            dbContext.Organizations.Add(new Organization
            {
                Id = organizationId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/developer/organizations/{organizationId}/memberships");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Verifies owners can list organization memberships.
    /// </summary>
    [Fact]
    public async Task ListMembershipsEndpoint_WithOwnerMembership_ReturnsMembers()
    {
        var organizationId = Guid.NewGuid();
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
            dbContext.Organizations.Add(new Organization
            {
                Id = organizationId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.OrganizationMemberships.AddRange(
                new OrganizationMembership
                {
                    OrganizationId = organizationId,
                    UserId = ownerUserId,
                    Role = "owner",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new OrganizationMembership
                {
                    OrganizationId = organizationId,
                    UserId = editorUserId,
                    Role = "editor",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/developer/organizations/{organizationId}/memberships");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var memberships = document.RootElement.GetProperty("memberships").EnumerateArray().ToList();

        Assert.Equal(2, memberships.Count);
        Assert.Contains(memberships, membership => membership.GetProperty("keycloakSubject").GetString() == "editor-456");
    }

    /// <summary>
    /// Verifies membership listing returns not found for missing organizations.
    /// </summary>
    [Fact]
    public async Task ListMembershipsEndpoint_WhenOrganizationIsMissing_ReturnsNotFound()
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
        using var response = await client.GetAsync($"/developer/organizations/{Guid.NewGuid()}/memberships");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies owners can add or update memberships by target Keycloak subject.
    /// </summary>
    [Fact]
    public async Task UpsertMembershipEndpoint_WithOwnerMembership_PersistsMembership()
    {
        var organizationId = Guid.NewGuid();
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
                UserId = ownerUserId,
                Role = "owner",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.PutAsJsonAsync(
            $"/developer/organizations/{organizationId}/memberships/editor-456",
            new
            {
                role = "editor"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();
        var membership = await verificationDbContext.OrganizationMemberships
            .Include(candidate => candidate.User)
            .SingleAsync(candidate => candidate.OrganizationId == organizationId && candidate.User.KeycloakSubject == "editor-456");

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
            $"/developer/organizations/{Guid.NewGuid()}/memberships/editor-456",
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
        var organizationId = Guid.NewGuid();
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
                UserId = ownerUserId,
                Role = "owner",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.PutAsJsonAsync(
            $"/developer/organizations/{organizationId}/memberships/missing-user",
            new
            {
                role = "editor"
            });
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("organization_member_target_not_found", document.RootElement.GetProperty("code").GetString());
    }

    /// <summary>
    /// Verifies non-managers cannot change memberships.
    /// </summary>
    [Fact]
    public async Task UpsertMembershipEndpoint_WithoutManagementMembership_ReturnsForbidden()
    {
        var organizationId = Guid.NewGuid();
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
                UserId = editorUserId,
                Role = "editor",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.PutAsJsonAsync(
            $"/developer/organizations/{organizationId}/memberships/owner-456",
            new
            {
                role = "admin"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Verifies the last owner cannot be removed from an organization.
    /// </summary>
    [Fact]
    public async Task DeleteMembershipEndpoint_WhenTargetIsLastOwner_ReturnsConflict()
    {
        var organizationId = Guid.NewGuid();
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
                UserId = ownerUserId,
                Role = "owner",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.DeleteAsync($"/developer/organizations/{organizationId}/memberships/owner-123");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>
    /// Verifies membership deletion returns not found when the target membership does not exist.
    /// </summary>
    [Fact]
    public async Task DeleteMembershipEndpoint_WhenMembershipIsMissing_ReturnsNotFound()
    {
        var organizationId = Guid.NewGuid();
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
                UserId = ownerUserId,
                Role = "owner",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.DeleteAsync($"/developer/organizations/{organizationId}/memberships/missing-user");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies non-managers cannot delete memberships.
    /// </summary>
    [Fact]
    public async Task DeleteMembershipEndpoint_WithoutManagementMembership_ReturnsForbidden()
    {
        var organizationId = Guid.NewGuid();
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
            dbContext.Organizations.Add(new Organization
            {
                Id = organizationId,
                Slug = "stellar-forge",
                DisplayName = "Stellar Forge",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            dbContext.OrganizationMemberships.AddRange(
                new OrganizationMembership
                {
                    OrganizationId = organizationId,
                    UserId = editorUserId,
                    Role = "editor",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new OrganizationMembership
                {
                    OrganizationId = organizationId,
                    UserId = ownerUserId,
                    Role = "owner",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.DeleteAsync($"/developer/organizations/{organizationId}/memberships/owner-456");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Verifies owners can hard delete organizations.
    /// </summary>
    [Fact]
    public async Task DeleteOrganizationEndpoint_WithOwnerMembership_RemovesOrganization()
    {
        var organizationId = Guid.NewGuid();
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
                UserId = ownerUserId,
                Role = "owner",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.DeleteAsync($"/developer/organizations/{organizationId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();

        Assert.False(await verificationDbContext.Organizations.AnyAsync(candidate => candidate.Id == organizationId));
        Assert.False(await verificationDbContext.OrganizationMemberships.AnyAsync(candidate => candidate.OrganizationId == organizationId));
    }

    /// <summary>
    /// Verifies only owners can delete organizations.
    /// </summary>
    [Fact]
    public async Task DeleteOrganizationEndpoint_WithoutOwnerMembership_ReturnsForbidden()
    {
        var organizationId = Guid.NewGuid();
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
                UserId = adminUserId,
                Role = "admin",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.DeleteAsync($"/developer/organizations/{organizationId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Verifies delete requests return not found for missing organizations.
    /// </summary>
    [Fact]
    public async Task DeleteOrganizationEndpoint_WhenOrganizationIsMissing_ReturnsNotFound()
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
        using var response = await client.DeleteAsync($"/developer/organizations/{Guid.NewGuid()}");

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
}
