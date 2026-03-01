using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Board.ThirdPartyLibrary.Api.Persistence;

internal sealed class BoardLibraryDbContextFactory : IDesignTimeDbContextFactory<BoardLibraryDbContext>
{
    public BoardLibraryDbContext CreateDbContext(string[] args)
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("BoardLibrary");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:BoardLibrary must be configured for design-time EF Core operations.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<BoardLibraryDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new BoardLibraryDbContext(optionsBuilder.Options);
    }
}
