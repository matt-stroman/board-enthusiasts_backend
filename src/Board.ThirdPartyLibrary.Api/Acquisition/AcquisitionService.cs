using System.Security.Claims;
using System.Text.Json;
using Board.ThirdPartyLibrary.Api.Auth;
using Board.ThirdPartyLibrary.Api.Identity;
using Board.ThirdPartyLibrary.Api.Studios;
using Board.ThirdPartyLibrary.Api.Persistence;
using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Board.ThirdPartyLibrary.Api.Acquisition;

/// <summary>
/// Service contract for supported publisher discovery and authenticated acquisition configuration management.
/// </summary>
internal interface IAcquisitionService
{
    Task<IReadOnlyList<SupportedPublisherSnapshot>> ListSupportedPublishersAsync(CancellationToken cancellationToken = default);

    Task<IntegrationConnectionListResult> ListIntegrationConnectionsAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        CancellationToken cancellationToken = default);

    Task<IntegrationConnectionMutationResult> CreateIntegrationConnectionAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        UpsertIntegrationConnectionCommand command,
        CancellationToken cancellationToken = default);

    Task<IntegrationConnectionMutationResult> GetIntegrationConnectionAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        Guid connectionId,
        CancellationToken cancellationToken = default);

    Task<IntegrationConnectionMutationResult> UpdateIntegrationConnectionAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        Guid connectionId,
        UpsertIntegrationConnectionCommand command,
        CancellationToken cancellationToken = default);

    Task<IntegrationConnectionDeleteResult> DeleteIntegrationConnectionAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        Guid connectionId,
        CancellationToken cancellationToken = default);

    Task<TitleIntegrationBindingListResult> ListTitleIntegrationBindingsAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        CancellationToken cancellationToken = default);

    Task<TitleIntegrationBindingMutationResult> CreateTitleIntegrationBindingAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        UpsertTitleIntegrationBindingCommand command,
        CancellationToken cancellationToken = default);

    Task<TitleIntegrationBindingMutationResult> GetTitleIntegrationBindingAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid bindingId,
        CancellationToken cancellationToken = default);

    Task<TitleIntegrationBindingMutationResult> UpdateTitleIntegrationBindingAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid bindingId,
        UpsertTitleIntegrationBindingCommand command,
        CancellationToken cancellationToken = default);

    Task<TitleIntegrationBindingDeleteResult> DeleteTitleIntegrationBindingAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid bindingId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Entity Framework-backed implementation of <see cref="IAcquisitionService" />.
/// </summary>
internal sealed class AcquisitionService(
    BoardLibraryDbContext dbContext,
    IIdentityPersistenceService identityPersistenceService) : IAcquisitionService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SupportedPublisherSnapshot>> ListSupportedPublishersAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SupportedPublishers
            .AsNoTracking()
            .Where(candidate => candidate.IsEnabled)
            .OrderBy(candidate => candidate.DisplayName)
            .Select(candidate => MapSupportedPublisher(candidate))
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IntegrationConnectionListResult> ListIntegrationConnectionsAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedStudioAsync(claims, studioId, cancellationToken);
        if (access.Status is not AcquisitionAccessStatus.Success)
        {
            return access.Status == AcquisitionAccessStatus.Forbidden
                ? new IntegrationConnectionListResult(AcquisitionListStatus.Forbidden)
                : new IntegrationConnectionListResult(AcquisitionListStatus.NotFound);
        }

        var connections = await dbContext.IntegrationConnections
            .AsNoTracking()
            .Include(candidate => candidate.SupportedPublisher)
            .Where(candidate => candidate.StudioId == studioId)
            .OrderBy(candidate => candidate.SupportedPublisherId == null)
            .ThenBy(candidate => candidate.SupportedPublisher != null ? candidate.SupportedPublisher.DisplayName : candidate.CustomPublisherDisplayName)
            .ToListAsync(cancellationToken);

        return new IntegrationConnectionListResult(
            AcquisitionListStatus.Success,
            connections.Select(MapIntegrationConnection).ToList());
    }

    /// <inheritdoc />
    public async Task<IntegrationConnectionMutationResult> CreateIntegrationConnectionAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        UpsertIntegrationConnectionCommand command,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedStudioAsync(claims, studioId, cancellationToken);
        if (access.Status is not AcquisitionAccessStatus.Success)
        {
            return access.Status == AcquisitionAccessStatus.Forbidden
                ? new IntegrationConnectionMutationResult(AcquisitionMutationStatus.Forbidden)
                : new IntegrationConnectionMutationResult(AcquisitionMutationStatus.NotFound);
        }

        var supportedPublisher = await ResolveSupportedPublisherAsync(command.SupportedPublisherId, cancellationToken);
        if (command.SupportedPublisherId is not null && supportedPublisher is null)
        {
            return new IntegrationConnectionMutationResult(AcquisitionMutationStatus.NotFound);
        }

        var now = DateTime.UtcNow;
        var connection = new IntegrationConnection
        {
            Id = Guid.NewGuid(),
            StudioId = studioId,
            SupportedPublisherId = supportedPublisher?.Id,
            SupportedPublisher = supportedPublisher,
            CustomPublisherDisplayName = command.CustomPublisherDisplayName,
            CustomPublisherHomepageUrl = command.CustomPublisherHomepageUrl,
            ConfigurationJson = CloneJson(command.Configuration),
            IsEnabled = command.IsEnabled,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.IntegrationConnections.Add(connection);
        access.Studio!.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new IntegrationConnectionMutationResult(
            AcquisitionMutationStatus.Success,
            MapIntegrationConnection(connection));
    }

    /// <inheritdoc />
    public async Task<IntegrationConnectionMutationResult> GetIntegrationConnectionAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedStudioAsync(claims, studioId, cancellationToken);
        if (access.Status is not AcquisitionAccessStatus.Success)
        {
            return access.Status == AcquisitionAccessStatus.Forbidden
                ? new IntegrationConnectionMutationResult(AcquisitionMutationStatus.Forbidden)
                : new IntegrationConnectionMutationResult(AcquisitionMutationStatus.NotFound);
        }

        var connection = await dbContext.IntegrationConnections
            .AsNoTracking()
            .Include(candidate => candidate.SupportedPublisher)
            .SingleOrDefaultAsync(
                candidate => candidate.StudioId == studioId && candidate.Id == connectionId,
                cancellationToken);

        return connection is null
            ? new IntegrationConnectionMutationResult(AcquisitionMutationStatus.NotFound)
            : new IntegrationConnectionMutationResult(
                AcquisitionMutationStatus.Success,
                MapIntegrationConnection(connection));
    }

    /// <inheritdoc />
    public async Task<IntegrationConnectionMutationResult> UpdateIntegrationConnectionAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        Guid connectionId,
        UpsertIntegrationConnectionCommand command,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedStudioAsync(claims, studioId, cancellationToken);
        if (access.Status is not AcquisitionAccessStatus.Success)
        {
            return access.Status == AcquisitionAccessStatus.Forbidden
                ? new IntegrationConnectionMutationResult(AcquisitionMutationStatus.Forbidden)
                : new IntegrationConnectionMutationResult(AcquisitionMutationStatus.NotFound);
        }

        var connection = await dbContext.IntegrationConnections
            .Include(candidate => candidate.SupportedPublisher)
            .SingleOrDefaultAsync(
                candidate => candidate.StudioId == studioId && candidate.Id == connectionId,
                cancellationToken);

        if (connection is null)
        {
            return new IntegrationConnectionMutationResult(AcquisitionMutationStatus.NotFound);
        }

        var supportedPublisher = await ResolveSupportedPublisherAsync(command.SupportedPublisherId, cancellationToken);
        if (command.SupportedPublisherId is not null && supportedPublisher is null)
        {
            return new IntegrationConnectionMutationResult(AcquisitionMutationStatus.NotFound);
        }

        var now = DateTime.UtcNow;
        connection.SupportedPublisherId = supportedPublisher?.Id;
        connection.SupportedPublisher = supportedPublisher;
        connection.CustomPublisherDisplayName = command.CustomPublisherDisplayName;
        connection.CustomPublisherHomepageUrl = command.CustomPublisherHomepageUrl;
        connection.ConfigurationJson = CloneJson(command.Configuration);
        connection.IsEnabled = command.IsEnabled;
        connection.UpdatedAtUtc = now;
        access.Studio!.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return new IntegrationConnectionMutationResult(
            AcquisitionMutationStatus.Success,
            MapIntegrationConnection(connection));
    }

    /// <inheritdoc />
    public async Task<IntegrationConnectionDeleteResult> DeleteIntegrationConnectionAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedStudioAsync(claims, studioId, cancellationToken);
        if (access.Status is not AcquisitionAccessStatus.Success)
        {
            return access.Status == AcquisitionAccessStatus.Forbidden
                ? new IntegrationConnectionDeleteResult(AcquisitionMutationStatus.Forbidden)
                : new IntegrationConnectionDeleteResult(AcquisitionMutationStatus.NotFound);
        }

        var connection = await dbContext.IntegrationConnections
            .SingleOrDefaultAsync(
                candidate => candidate.StudioId == studioId && candidate.Id == connectionId,
                cancellationToken);

        if (connection is null)
        {
            return new IntegrationConnectionDeleteResult(AcquisitionMutationStatus.NotFound);
        }

        var isInUse = await dbContext.TitleIntegrationBindings
            .AsNoTracking()
            .AnyAsync(candidate => candidate.IntegrationConnectionId == connectionId, cancellationToken);

        if (isInUse)
        {
            return new IntegrationConnectionDeleteResult(
                AcquisitionMutationStatus.Conflict,
                AcquisitionErrorCodes.IntegrationConnectionInUse);
        }

        dbContext.IntegrationConnections.Remove(connection);
        access.Studio!.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new IntegrationConnectionDeleteResult(AcquisitionMutationStatus.Success);
    }

    /// <inheritdoc />
    public async Task<TitleIntegrationBindingListResult> ListTitleIntegrationBindingsAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not AcquisitionAccessStatus.Success)
        {
            return access.Status == AcquisitionAccessStatus.Forbidden
                ? new TitleIntegrationBindingListResult(AcquisitionListStatus.Forbidden)
                : new TitleIntegrationBindingListResult(AcquisitionListStatus.NotFound);
        }

        var bindings = await dbContext.TitleIntegrationBindings
            .AsNoTracking()
            .Include(candidate => candidate.IntegrationConnection)
                .ThenInclude(candidate => candidate.SupportedPublisher)
            .Where(candidate => candidate.TitleId == titleId)
            .OrderByDescending(candidate => candidate.IsPrimary)
            .ThenBy(candidate => candidate.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return new TitleIntegrationBindingListResult(
            AcquisitionListStatus.Success,
            bindings.Select(MapTitleIntegrationBinding).ToList());
    }

    /// <inheritdoc />
    public async Task<TitleIntegrationBindingMutationResult> CreateTitleIntegrationBindingAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        UpsertTitleIntegrationBindingCommand command,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not AcquisitionAccessStatus.Success)
        {
            return access.Status == AcquisitionAccessStatus.Forbidden
                ? new TitleIntegrationBindingMutationResult(AcquisitionMutationStatus.Forbidden)
                : new TitleIntegrationBindingMutationResult(AcquisitionMutationStatus.NotFound);
        }

        var title = access.Title!;
        var connection = await dbContext.IntegrationConnections
            .Include(candidate => candidate.SupportedPublisher)
            .SingleOrDefaultAsync(candidate => candidate.Id == command.IntegrationConnectionId, cancellationToken);

        if (connection is null)
        {
            return new TitleIntegrationBindingMutationResult(AcquisitionMutationStatus.NotFound);
        }

        if (connection.StudioId != title.StudioId)
        {
            return new TitleIntegrationBindingMutationResult(
                AcquisitionMutationStatus.Conflict,
                ErrorCode: AcquisitionErrorCodes.TitleIntegrationStudioConflict);
        }

        if (command.IsEnabled && !connection.IsEnabled)
        {
            return new TitleIntegrationBindingMutationResult(
                AcquisitionMutationStatus.Conflict,
                ErrorCode: AcquisitionErrorCodes.TitleIntegrationConnectionDisabled);
        }

        var existingBindings = await dbContext.TitleIntegrationBindings
            .Where(candidate => candidate.TitleId == titleId)
            .ToListAsync(cancellationToken);

        if (command.IsEnabled && !command.IsPrimary && existingBindings.All(candidate => !candidate.IsEnabled))
        {
            return new TitleIntegrationBindingMutationResult(
                AcquisitionMutationStatus.Conflict,
                ErrorCode: AcquisitionErrorCodes.TitleIntegrationPrimaryRequired);
        }

        var now = DateTime.UtcNow;
        if (command.IsEnabled && command.IsPrimary)
        {
            foreach (var existingPrimary in existingBindings.Where(candidate => candidate.IsEnabled && candidate.IsPrimary))
            {
                existingPrimary.IsPrimary = false;
                existingPrimary.UpdatedAtUtc = now;
            }
        }

        var binding = new TitleIntegrationBinding
        {
            Id = Guid.NewGuid(),
            TitleId = titleId,
            IntegrationConnectionId = connection.Id,
            IntegrationConnection = connection,
            AcquisitionUrl = command.AcquisitionUrl,
            AcquisitionLabel = command.AcquisitionLabel,
            ConfigurationJson = CloneJson(command.Configuration),
            IsPrimary = command.IsPrimary,
            IsEnabled = command.IsEnabled,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.TitleIntegrationBindings.Add(binding);
        title.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new TitleIntegrationBindingMutationResult(
            AcquisitionMutationStatus.Success,
            MapTitleIntegrationBinding(binding));
    }

    /// <inheritdoc />
    public async Task<TitleIntegrationBindingMutationResult> GetTitleIntegrationBindingAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid bindingId,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not AcquisitionAccessStatus.Success)
        {
            return access.Status == AcquisitionAccessStatus.Forbidden
                ? new TitleIntegrationBindingMutationResult(AcquisitionMutationStatus.Forbidden)
                : new TitleIntegrationBindingMutationResult(AcquisitionMutationStatus.NotFound);
        }

        var binding = await dbContext.TitleIntegrationBindings
            .AsNoTracking()
            .Include(candidate => candidate.IntegrationConnection)
                .ThenInclude(candidate => candidate.SupportedPublisher)
            .SingleOrDefaultAsync(
                candidate => candidate.TitleId == titleId && candidate.Id == bindingId,
                cancellationToken);

        return binding is null
            ? new TitleIntegrationBindingMutationResult(AcquisitionMutationStatus.NotFound)
            : new TitleIntegrationBindingMutationResult(
                AcquisitionMutationStatus.Success,
                MapTitleIntegrationBinding(binding));
    }

    /// <inheritdoc />
    public async Task<TitleIntegrationBindingMutationResult> UpdateTitleIntegrationBindingAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid bindingId,
        UpsertTitleIntegrationBindingCommand command,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not AcquisitionAccessStatus.Success)
        {
            return access.Status == AcquisitionAccessStatus.Forbidden
                ? new TitleIntegrationBindingMutationResult(AcquisitionMutationStatus.Forbidden)
                : new TitleIntegrationBindingMutationResult(AcquisitionMutationStatus.NotFound);
        }

        var title = access.Title!;
        var binding = await dbContext.TitleIntegrationBindings
            .Include(candidate => candidate.IntegrationConnection)
                .ThenInclude(candidate => candidate.SupportedPublisher)
            .SingleOrDefaultAsync(
                candidate => candidate.TitleId == titleId && candidate.Id == bindingId,
                cancellationToken);

        if (binding is null)
        {
            return new TitleIntegrationBindingMutationResult(AcquisitionMutationStatus.NotFound);
        }

        var connection = await dbContext.IntegrationConnections
            .Include(candidate => candidate.SupportedPublisher)
            .SingleOrDefaultAsync(candidate => candidate.Id == command.IntegrationConnectionId, cancellationToken);

        if (connection is null)
        {
            return new TitleIntegrationBindingMutationResult(AcquisitionMutationStatus.NotFound);
        }

        if (connection.StudioId != title.StudioId)
        {
            return new TitleIntegrationBindingMutationResult(
                AcquisitionMutationStatus.Conflict,
                ErrorCode: AcquisitionErrorCodes.TitleIntegrationStudioConflict);
        }

        if (command.IsEnabled && !connection.IsEnabled)
        {
            return new TitleIntegrationBindingMutationResult(
                AcquisitionMutationStatus.Conflict,
                ErrorCode: AcquisitionErrorCodes.TitleIntegrationConnectionDisabled);
        }

        var siblings = await dbContext.TitleIntegrationBindings
            .Where(candidate => candidate.TitleId == titleId && candidate.Id != bindingId)
            .ToListAsync(cancellationToken);

        var enabledSiblingCount = siblings.Count(candidate => candidate.IsEnabled);
        var hasEnabledSiblingPrimary = siblings.Any(candidate => candidate.IsEnabled && candidate.IsPrimary);

        if (command.IsEnabled && !command.IsPrimary && enabledSiblingCount == 0)
        {
            return new TitleIntegrationBindingMutationResult(
                AcquisitionMutationStatus.Conflict,
                ErrorCode: AcquisitionErrorCodes.TitleIntegrationPrimaryRequired);
        }

        if (!command.IsPrimary && command.IsEnabled && binding.IsPrimary && !hasEnabledSiblingPrimary)
        {
            return new TitleIntegrationBindingMutationResult(
                AcquisitionMutationStatus.Conflict,
                ErrorCode: AcquisitionErrorCodes.TitleIntegrationPrimaryRequired);
        }

        if (!command.IsEnabled && binding.IsPrimary && enabledSiblingCount > 0)
        {
            return new TitleIntegrationBindingMutationResult(
                AcquisitionMutationStatus.Conflict,
                ErrorCode: AcquisitionErrorCodes.TitleIntegrationPrimaryRequired);
        }

        var updatedAt = DateTime.UtcNow;
        if (command.IsEnabled && command.IsPrimary)
        {
            foreach (var sibling in siblings.Where(candidate => candidate.IsEnabled && candidate.IsPrimary))
            {
                sibling.IsPrimary = false;
                sibling.UpdatedAtUtc = updatedAt;
            }
        }

        binding.IntegrationConnectionId = connection.Id;
        binding.IntegrationConnection = connection;
        binding.AcquisitionUrl = command.AcquisitionUrl;
        binding.AcquisitionLabel = command.AcquisitionLabel;
        binding.ConfigurationJson = CloneJson(command.Configuration);
        binding.IsPrimary = command.IsPrimary;
        binding.IsEnabled = command.IsEnabled;
        binding.UpdatedAtUtc = updatedAt;
        title.UpdatedAtUtc = updatedAt;

        await dbContext.SaveChangesAsync(cancellationToken);
        return new TitleIntegrationBindingMutationResult(
            AcquisitionMutationStatus.Success,
            MapTitleIntegrationBinding(binding));
    }

    /// <inheritdoc />
    public async Task<TitleIntegrationBindingDeleteResult> DeleteTitleIntegrationBindingAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid bindingId,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not AcquisitionAccessStatus.Success)
        {
            return access.Status == AcquisitionAccessStatus.Forbidden
                ? new TitleIntegrationBindingDeleteResult(AcquisitionMutationStatus.Forbidden)
                : new TitleIntegrationBindingDeleteResult(AcquisitionMutationStatus.NotFound);
        }

        var binding = await dbContext.TitleIntegrationBindings
            .SingleOrDefaultAsync(
                candidate => candidate.TitleId == titleId && candidate.Id == bindingId,
                cancellationToken);

        if (binding is null)
        {
            return new TitleIntegrationBindingDeleteResult(AcquisitionMutationStatus.NotFound);
        }

        if (binding.IsEnabled && binding.IsPrimary)
        {
            var hasOtherEnabledBindings = await dbContext.TitleIntegrationBindings
                .AsNoTracking()
                .AnyAsync(
                    candidate => candidate.TitleId == titleId && candidate.Id != bindingId && candidate.IsEnabled,
                    cancellationToken);

            if (hasOtherEnabledBindings)
            {
                return new TitleIntegrationBindingDeleteResult(
                    AcquisitionMutationStatus.Conflict,
                    AcquisitionErrorCodes.TitleIntegrationPrimaryRequired);
            }
        }

        dbContext.TitleIntegrationBindings.Remove(binding);
        access.Title!.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new TitleIntegrationBindingDeleteResult(AcquisitionMutationStatus.Success);
    }

    private async Task<SupportedPublisher?> ResolveSupportedPublisherAsync(Guid? supportedPublisherId, CancellationToken cancellationToken) =>
        supportedPublisherId is null
            ? null
            : await dbContext.SupportedPublishers
                .SingleOrDefaultAsync(candidate => candidate.Id == supportedPublisherId && candidate.IsEnabled, cancellationToken);

    private async Task<AcquisitionStudioAccessResult> LoadManagedStudioAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        CancellationToken cancellationToken)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var studio = await dbContext.Studios
            .SingleOrDefaultAsync(candidate => candidate.Id == studioId, cancellationToken);

        if (studio is null)
        {
            return new AcquisitionStudioAccessResult(AcquisitionAccessStatus.NotFound);
        }

        var actorRole = await GetActorStudioRoleAsync(studioId, actor.Id, cancellationToken);
        if (!CanManageAcquisition(actorRole))
        {
            return new AcquisitionStudioAccessResult(AcquisitionAccessStatus.Forbidden);
        }

        return new AcquisitionStudioAccessResult(AcquisitionAccessStatus.Success, studio);
    }

    private async Task<AcquisitionTitleAccessResult> LoadManagedTitleAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        CancellationToken cancellationToken)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var title = await dbContext.Titles
            .Include(candidate => candidate.Studio)
            .SingleOrDefaultAsync(candidate => candidate.Id == titleId, cancellationToken);

        if (title is null)
        {
            return new AcquisitionTitleAccessResult(AcquisitionAccessStatus.NotFound);
        }

        var actorRole = await GetActorStudioRoleAsync(title.StudioId, actor.Id, cancellationToken);
        if (!CanManageAcquisition(actorRole))
        {
            return new AcquisitionTitleAccessResult(AcquisitionAccessStatus.Forbidden);
        }

        return new AcquisitionTitleAccessResult(AcquisitionAccessStatus.Success, title);
    }

    private async Task<AppUser> EnsureActorAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        await identityPersistenceService.EnsureCurrentUserProjectionAsync(claims, cancellationToken);
        var subject = ClaimValueResolver.GetSubject(claims);

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new InvalidOperationException("Authenticated user is missing the required subject claim.");
        }

        return await dbContext.Users.SingleAsync(candidate => candidate.KeycloakSubject == subject, cancellationToken);
    }

    private async Task<string?> GetActorStudioRoleAsync(Guid studioId, Guid actorUserId, CancellationToken cancellationToken) =>
        await dbContext.StudioMemberships
            .Where(candidate => candidate.StudioId == studioId && candidate.UserId == actorUserId)
            .Select(candidate => candidate.Role)
            .SingleOrDefaultAsync(cancellationToken);

    private static bool CanManageAcquisition(string? role) =>
        string.Equals(role, StudioRoles.Owner, StringComparison.Ordinal) ||
        string.Equals(role, StudioRoles.Admin, StringComparison.Ordinal) ||
        string.Equals(role, StudioRoles.Editor, StringComparison.Ordinal);

    private static string? CloneJson(JsonElement? configuration) =>
        configuration is null || configuration.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? null
            : configuration.Value.GetRawText();

    private static JsonElement? ParseJsonElement(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(configurationJson);
        return document.RootElement.Clone();
    }

    private static SupportedPublisherSnapshot MapSupportedPublisher(SupportedPublisher publisher) =>
        new(
            publisher.Id,
            publisher.Key,
            publisher.DisplayName,
            publisher.HomepageUrl);

    private static IntegrationConnectionSnapshot MapIntegrationConnection(IntegrationConnection connection) =>
        new(
            connection.Id,
            connection.StudioId,
            connection.SupportedPublisherId,
            connection.SupportedPublisher is null ? null : MapSupportedPublisher(connection.SupportedPublisher),
            connection.CustomPublisherDisplayName,
            connection.CustomPublisherHomepageUrl,
            ParseJsonElement(connection.ConfigurationJson),
            connection.IsEnabled,
            connection.CreatedAtUtc,
            connection.UpdatedAtUtc);

    private static TitleIntegrationBindingSnapshot MapTitleIntegrationBinding(TitleIntegrationBinding binding) =>
        new(
            binding.Id,
            binding.TitleId,
            binding.IntegrationConnectionId,
            MapIntegrationConnection(binding.IntegrationConnection),
            binding.AcquisitionUrl,
            binding.AcquisitionLabel,
            ParseJsonElement(binding.ConfigurationJson),
            binding.IsPrimary,
            binding.IsEnabled,
            binding.CreatedAtUtc,
            binding.UpdatedAtUtc);
}

/// <summary>
/// Command payload for creating or updating a studio integration connection.
/// </summary>
/// <param name="SupportedPublisherId">Canonical supported publisher identifier when using a registry entry.</param>
/// <param name="CustomPublisherDisplayName">Custom publisher display name when using a studio-owned custom connection.</param>
/// <param name="CustomPublisherHomepageUrl">Custom publisher homepage URL when using a studio-owned custom connection.</param>
/// <param name="Configuration">Optional provider-specific non-secret configuration object.</param>
/// <param name="IsEnabled">Whether the connection can be selected for enabled bindings.</param>
internal sealed record UpsertIntegrationConnectionCommand(
    Guid? SupportedPublisherId,
    string? CustomPublisherDisplayName,
    string? CustomPublisherHomepageUrl,
    JsonElement? Configuration,
    bool IsEnabled);

/// <summary>
/// Command payload for creating or updating a title acquisition binding.
/// </summary>
/// <param name="IntegrationConnectionId">Referenced integration connection identifier.</param>
/// <param name="AcquisitionUrl">Player-facing external acquisition URL.</param>
/// <param name="AcquisitionLabel">Optional player-facing acquisition label.</param>
/// <param name="Configuration">Optional provider-specific non-secret configuration object.</param>
/// <param name="IsPrimary">Whether the binding is the active primary binding for the title.</param>
/// <param name="IsEnabled">Whether the binding is available to players.</param>
internal sealed record UpsertTitleIntegrationBindingCommand(
    Guid IntegrationConnectionId,
    string AcquisitionUrl,
    string? AcquisitionLabel,
    JsonElement? Configuration,
    bool IsPrimary,
    bool IsEnabled);

/// <summary>
/// Projection of a supported publisher registry entry.
/// </summary>
/// <param name="Id">Supported publisher identifier.</param>
/// <param name="Key">Stable machine-friendly publisher key.</param>
/// <param name="DisplayName">Public publisher name.</param>
/// <param name="HomepageUrl">Canonical publisher homepage URL.</param>
internal sealed record SupportedPublisherSnapshot(
    Guid Id,
    string Key,
    string DisplayName,
    string HomepageUrl);

/// <summary>
/// Projection of a studio-owned integration connection.
/// </summary>
/// <param name="Id">Integration connection identifier.</param>
/// <param name="StudioId">Owning studio identifier.</param>
/// <param name="SupportedPublisherId">Linked supported publisher identifier when present.</param>
/// <param name="SupportedPublisher">Canonical supported publisher details when present.</param>
/// <param name="CustomPublisherDisplayName">Custom publisher display name when using a custom connection.</param>
/// <param name="CustomPublisherHomepageUrl">Custom publisher homepage URL when using a custom connection.</param>
/// <param name="Configuration">Optional provider-specific non-secret configuration object.</param>
/// <param name="IsEnabled">Whether the connection can be selected for enabled bindings.</param>
/// <param name="CreatedAtUtc">UTC creation timestamp.</param>
/// <param name="UpdatedAtUtc">UTC update timestamp.</param>
internal sealed record IntegrationConnectionSnapshot(
    Guid Id,
    Guid StudioId,
    Guid? SupportedPublisherId,
    SupportedPublisherSnapshot? SupportedPublisher,
    string? CustomPublisherDisplayName,
    string? CustomPublisherHomepageUrl,
    JsonElement? Configuration,
    bool IsEnabled,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>
/// Projection of a title acquisition binding.
/// </summary>
/// <param name="Id">Binding identifier.</param>
/// <param name="TitleId">Owning title identifier.</param>
/// <param name="IntegrationConnectionId">Referenced integration connection identifier.</param>
/// <param name="IntegrationConnection">Referenced integration connection details.</param>
/// <param name="AcquisitionUrl">Player-facing external acquisition URL.</param>
/// <param name="AcquisitionLabel">Optional player-facing acquisition label.</param>
/// <param name="Configuration">Optional provider-specific non-secret configuration object.</param>
/// <param name="IsPrimary">Whether the binding is the active primary binding for the title.</param>
/// <param name="IsEnabled">Whether the binding is available to players.</param>
/// <param name="CreatedAtUtc">UTC creation timestamp.</param>
/// <param name="UpdatedAtUtc">UTC update timestamp.</param>
internal sealed record TitleIntegrationBindingSnapshot(
    Guid Id,
    Guid TitleId,
    Guid IntegrationConnectionId,
    IntegrationConnectionSnapshot IntegrationConnection,
    string AcquisitionUrl,
    string? AcquisitionLabel,
    JsonElement? Configuration,
    bool IsPrimary,
    bool IsEnabled,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>
/// Result wrapper for integration connection listings.
/// </summary>
/// <param name="Status">Operation status.</param>
/// <param name="IntegrationConnections">Returned integration connections when available.</param>
internal sealed record IntegrationConnectionListResult(
    AcquisitionListStatus Status,
    IReadOnlyList<IntegrationConnectionSnapshot>? IntegrationConnections = null);

/// <summary>
/// Result wrapper for single integration connection operations.
/// </summary>
/// <param name="Status">Operation status.</param>
/// <param name="IntegrationConnection">Returned integration connection when available.</param>
/// <param name="ErrorCode">Optional machine-readable conflict code.</param>
internal sealed record IntegrationConnectionMutationResult(
    AcquisitionMutationStatus Status,
    IntegrationConnectionSnapshot? IntegrationConnection = null,
    string? ErrorCode = null);

/// <summary>
/// Result wrapper for integration connection deletion.
/// </summary>
/// <param name="Status">Operation status.</param>
/// <param name="ErrorCode">Optional machine-readable conflict code.</param>
internal sealed record IntegrationConnectionDeleteResult(
    AcquisitionMutationStatus Status,
    string? ErrorCode = null);

/// <summary>
/// Result wrapper for title acquisition binding listings.
/// </summary>
/// <param name="Status">Operation status.</param>
/// <param name="IntegrationBindings">Returned title acquisition bindings when available.</param>
internal sealed record TitleIntegrationBindingListResult(
    AcquisitionListStatus Status,
    IReadOnlyList<TitleIntegrationBindingSnapshot>? IntegrationBindings = null);

/// <summary>
/// Result wrapper for single title acquisition binding operations.
/// </summary>
/// <param name="Status">Operation status.</param>
/// <param name="IntegrationBinding">Returned title acquisition binding when available.</param>
/// <param name="ErrorCode">Optional machine-readable conflict code.</param>
internal sealed record TitleIntegrationBindingMutationResult(
    AcquisitionMutationStatus Status,
    TitleIntegrationBindingSnapshot? IntegrationBinding = null,
    string? ErrorCode = null);

/// <summary>
/// Result wrapper for title acquisition binding deletion.
/// </summary>
/// <param name="Status">Operation status.</param>
/// <param name="ErrorCode">Optional machine-readable conflict code.</param>
internal sealed record TitleIntegrationBindingDeleteResult(
    AcquisitionMutationStatus Status,
    string? ErrorCode = null);

/// <summary>
/// Outcome codes for acquisition mutations.
/// </summary>
internal enum AcquisitionMutationStatus
{
    Success,
    NotFound,
    Forbidden,
    Conflict
}

/// <summary>
/// Outcome codes for acquisition listings.
/// </summary>
internal enum AcquisitionListStatus
{
    Success,
    NotFound,
    Forbidden
}

/// <summary>
/// Internal access resolution status for acquisition operations.
/// </summary>
internal enum AcquisitionAccessStatus
{
    Success,
    NotFound,
    Forbidden
}

/// <summary>
/// Authorized studio access resolution result.
/// </summary>
/// <param name="Status">Access resolution outcome.</param>
/// <param name="Studio">Resolved studio when access succeeds.</param>
internal sealed record AcquisitionStudioAccessResult(
    AcquisitionAccessStatus Status,
    Studio? Studio = null);

/// <summary>
/// Authorized title access resolution result.
/// </summary>
/// <param name="Status">Access resolution outcome.</param>
/// <param name="Title">Resolved title when access succeeds.</param>
internal sealed record AcquisitionTitleAccessResult(
    AcquisitionAccessStatus Status,
    Title? Title = null);

/// <summary>
/// Machine-readable error codes emitted by acquisition operations.
/// </summary>
internal static class AcquisitionErrorCodes
{
    public const string IntegrationConnectionInUse = "integration_connection_in_use";
    public const string TitleIntegrationStudioConflict = "title_integration_studio_conflict";
    public const string TitleIntegrationPrimaryRequired = "title_integration_primary_required";
    public const string TitleIntegrationConnectionDisabled = "title_integration_connection_disabled";
}
