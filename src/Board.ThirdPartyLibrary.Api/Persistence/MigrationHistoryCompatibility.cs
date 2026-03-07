using Microsoft.EntityFrameworkCore;

namespace Board.ThirdPartyLibrary.Api.Persistence;

/// <summary>
/// Normalizes renamed migration identifiers for existing databases.
/// </summary>
internal static class MigrationHistoryCompatibility
{
    private static readonly IReadOnlyList<(string LegacyId, string CurrentId)> LegacyMigrationIds =
    [
        ("20260301204029_Wave2OrganizationsMemberships", "20260301204029_Wave2StudiosMemberships"),
        ("20260306031131_Wave7RenameOrganizationTablesToStudios", "20260306031131_Wave7RenameStudioTablesToStudios")
    ];

    /// <summary>
    /// Updates legacy migration identifiers in the EF history table when a local database still has pre-rename entries.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when normalization is done.</returns>
    public static async Task NormalizeAsync(BoardLibraryDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (legacyId, currentId) in LegacyMigrationIds)
        {
            if (!appliedMigrations.Contains(legacyId) || appliedMigrations.Contains(currentId))
            {
                continue;
            }

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "__EFMigrationsHistory" SET "MigrationId" = {currentId} WHERE "MigrationId" = {legacyId};""",
                cancellationToken);

            appliedMigrations.Remove(legacyId);
            appliedMigrations.Add(currentId);
        }
    }
}
