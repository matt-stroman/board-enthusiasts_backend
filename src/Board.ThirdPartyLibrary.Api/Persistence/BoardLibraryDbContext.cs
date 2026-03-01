using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Board.ThirdPartyLibrary.Api.Persistence;

internal sealed class BoardLibraryDbContext(DbContextOptions<BoardLibraryDbContext> options)
    : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<UserBoardProfile> UserBoardProfiles => Set<UserBoardProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BoardLibraryDbContext).Assembly);
    }
}
