using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Board.ThirdPartyLibrary.Api.IntegrationTests;

/// <summary>
/// Integration tests that verify readiness against a real PostgreSQL container.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ReadyHealthEndpointIntegrationTests : IAsyncLifetime
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
    /// Verifies the readiness endpoint returns healthy when a real PostgreSQL container is reachable.
    /// </summary>
    [Fact]
    public async Task ReadyHealthEndpoint_WithRealPostgres_ReturnsHealthy()
    {
        using var factory = new RealPostgresApiFactory(_postgresContainer.GetConnectionString());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.Equal("Healthy", root.GetProperty("status").GetString());

        var postgresCheck = root.GetProperty("checks")
            .EnumerateArray()
            .Single(check => check.GetProperty("name").GetString() == "postgres");

        Assert.Equal("Healthy", postgresCheck.GetProperty("status").GetString());
        Assert.Equal("board_tpl", postgresCheck.GetProperty("data").GetProperty("database").GetString());
        Assert.Equal("board_tpl_user", postgresCheck.GetProperty("data").GetProperty("user").GetString());
    }

    private sealed class RealPostgresApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;

        public RealPostgresApiFactory(string connectionString)
        {
            _connectionString = connectionString;
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
        }
    }
}
