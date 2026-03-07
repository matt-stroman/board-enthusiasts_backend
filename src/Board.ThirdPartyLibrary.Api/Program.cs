using System.Security.Claims;
using System.Net;
using System.Text.Json.Serialization;
using Board.ThirdPartyLibrary.Api.Acquisition;
using Board.ThirdPartyLibrary.Api.Auth;
using Board.ThirdPartyLibrary.Api.HealthChecks;
using Board.ThirdPartyLibrary.Api.Identity;
using Board.ThirdPartyLibrary.Api.Moderation;
using Board.ThirdPartyLibrary.Api.Players;
using Board.ThirdPartyLibrary.Api.Studios;
using Board.ThirdPartyLibrary.Api.Persistence;
using Board.ThirdPartyLibrary.Api.TitleReports;
using Board.ThirdPartyLibrary.Api.Titles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(endpointOptions =>
    {
        endpointOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services
    .AddOptions<KeycloakOptions>()
    .Bind(builder.Configuration.GetSection(KeycloakOptions.SectionName));

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IKeycloakEndpointResolver, KeycloakEndpointResolver>();
builder.Services.AddSingleton<IKeycloakAuthorizationStateStore, InMemoryKeycloakAuthorizationStateStore>();
builder.Services.AddTransient<IClaimsTransformation, KeycloakRoleClaimsTransformation>();
builder.Services.AddHttpClient<IKeycloakTokenClient, KeycloakTokenClient>(client =>
{
    client.DefaultRequestVersion = HttpVersion.Version20;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
});
builder.Services.AddHttpClient<IKeycloakUserRoleClient, KeycloakUserRoleClient>(client =>
{
    client.DefaultRequestVersion = HttpVersion.Version20;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
});

var keycloakOptions = builder.Configuration.GetSection(KeycloakOptions.SectionName).Get<KeycloakOptions>() ?? new KeycloakOptions();
var authority = $"{keycloakOptions.BaseUrl.TrimEnd('/')}/realms/{Uri.EscapeDataString(keycloakOptions.Realm)}";
var boardLibraryConnectionString = builder.Configuration.GetConnectionString("BoardLibrary");
var hasBoardLibraryConnectionString = !string.IsNullOrWhiteSpace(boardLibraryConnectionString);

builder.Services.AddDbContext<BoardLibraryDbContext>(options =>
    options.UseNpgsql(hasBoardLibraryConnectionString
        ? boardLibraryConnectionString
        : "Host=invalid;Port=5432;Database=board_tpl_unconfigured;Username=invalid;Password=invalid"));
builder.Services.AddScoped<IIdentityPersistenceService, IdentityPersistenceService>();
builder.Services.AddScoped<IUserNotificationService, UserNotificationService>();
builder.Services.AddScoped<IDeveloperEnrollmentService, DeveloperEnrollmentService>();
builder.Services.AddScoped<IAcquisitionService, AcquisitionService>();
builder.Services.AddScoped<IStudioService, StudioService>();
builder.Services.AddScoped<ITitleService, TitleService>();
builder.Services.AddScoped<IPlayerLibraryService, PlayerLibraryService>();
builder.Services.AddScoped<ITitleReportService, TitleReportService>();
builder.Services
    .AddOptions<StudioMediaStorageOptions>()
    .Bind(builder.Configuration.GetSection(StudioMediaStorageOptions.SectionName));
builder.Services.AddSingleton<IStudioMediaStorage, LocalStudioMediaStorage>();
builder.Services
    .AddOptions<TitleMediaStorageOptions>()
    .Bind(builder.Configuration.GetSection(TitleMediaStorageOptions.SectionName));
builder.Services.AddSingleton<ITitleMediaStorage, LocalTitleMediaStorage>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.RequireHttpsMetadata = keycloakOptions.RequireHttpsMetadata;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            NameClaimType = "preferred_username",
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddSingleton<IPostgresReadinessProbe, NpgsqlPostgresReadinessProbe>();

builder.Services.AddHealthChecks()
    .AddCheck<PostgresReadyHealthCheck>("postgres", tags: ["ready"]);

var app = builder.Build();

await ApplyDatabaseMigrationsAsync(app.Services, hasBoardLibraryConnectionString);

var studioMediaStorage = app.Services.GetRequiredService<IStudioMediaStorage>();
Directory.CreateDirectory(studioMediaStorage.RootPath);
var titleMediaStorage = app.Services.GetRequiredService<ITitleMediaStorage>();
Directory.CreateDirectory(titleMediaStorage.RootPath);

app.MapGet("/", () => Results.Ok(new
{
    service = "board-third-party-lib-backend",
    endpoints = new[] { "/health/live", "/health/ready" }
}));

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthResponseAsync
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponseAsync
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(studioMediaStorage.RootPath),
    RequestPath = "/uploads/studio-media"
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(titleMediaStorage.RootPath),
    RequestPath = "/uploads/title-media"
});

app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityEndpoints();
app.MapPlayerEndpoints();
app.MapModerationEndpoints();
app.MapAcquisitionEndpoints();
app.MapStudioEndpoints();
app.MapTitleEndpoints();
app.MapTitleReportEndpoints();

app.Run();

static async Task ApplyDatabaseMigrationsAsync(IServiceProvider services, bool applyRelationalMigrations)
{
    if (!applyRelationalMigrations)
    {
        return;
    }

    await using var scope = services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<BoardLibraryDbContext>();

    if (dbContext.Database.IsRelational())
    {
        await MigrationHistoryCompatibility.NormalizeAsync(dbContext);
        await LegacySchemaCompatibility.NormalizeAsync(dbContext);
        await dbContext.Database.MigrateAsync();
        return;
    }

    await dbContext.Database.EnsureCreatedAsync();
}

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    context.Response.StatusCode = report.Status == HealthStatus.Healthy
        ? StatusCodes.Status200OK
        : StatusCodes.Status503ServiceUnavailable;

    var response = new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
            data = entry.Value.Data.Count == 0 ? null : entry.Value.Data
        }),
        durationMs = report.TotalDuration.TotalMilliseconds
    };

    return context.Response.WriteAsJsonAsync(response);
}

/// <summary>
/// Entry point marker for integration and endpoint tests.
/// </summary>
public partial class Program;
