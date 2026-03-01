using System.Security.Claims;
using System.Text.Json.Serialization;
using Board.ThirdPartyLibrary.Api.Auth;
using Board.ThirdPartyLibrary.Api.HealthChecks;
using Board.ThirdPartyLibrary.Api.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddHttpClient<IKeycloakTokenClient, KeycloakTokenClient>();

var keycloakOptions = builder.Configuration.GetSection(KeycloakOptions.SectionName).Get<KeycloakOptions>() ?? new KeycloakOptions();
var authority = $"{keycloakOptions.BaseUrl.TrimEnd('/')}/realms/{Uri.EscapeDataString(keycloakOptions.Realm)}";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.RequireHttpsMetadata = keycloakOptions.RequireHttpsMetadata;
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

app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityEndpoints();

app.Run();

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
