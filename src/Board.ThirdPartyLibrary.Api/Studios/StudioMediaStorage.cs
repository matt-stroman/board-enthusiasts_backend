using Microsoft.Extensions.Options;

namespace Board.ThirdPartyLibrary.Api.Studios;

/// <summary>
/// Persists uploaded studio media files and returns backend-hosted route paths.
/// </summary>
internal interface IStudioMediaStorage
{
    /// <summary>
    /// Gets the absolute local filesystem root used for stored studio media files.
    /// </summary>
    string RootPath { get; }

    /// <summary>
    /// Saves uploaded studio media content and returns a route path served by this API.
    /// </summary>
    Task<string> SaveStudioMediaAsync(
        Guid studioId,
        string mediaRole,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for local studio media file storage.
/// </summary>
internal sealed class StudioMediaStorageOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "StudioMediaStorage";

    /// <summary>
    /// Default relative filesystem root for persisted studio media.
    /// </summary>
    public const string DefaultRootPath = "artifacts/studio-media";

    /// <summary>
    /// Gets or sets the local filesystem root path.
    /// </summary>
    public string RootPath { get; set; } = DefaultRootPath;
}

/// <summary>
/// Local filesystem-backed implementation of <see cref="IStudioMediaStorage" />.
/// </summary>
internal sealed class LocalStudioMediaStorage(
    IOptions<StudioMediaStorageOptions> options,
    IWebHostEnvironment environment) : IStudioMediaStorage
{
    /// <inheritdoc />
    public string RootPath { get; } = ResolveRootPath(options.Value.RootPath, environment.ContentRootPath);

    /// <inheritdoc />
    public async Task<string> SaveStudioMediaAsync(
        Guid studioId,
        string mediaRole,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        var extension = contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            "image/svg+xml" => ".svg",
            _ => ".bin"
        };

        var studioDirectory = Path.Combine(RootPath, studioId.ToString("D"), mediaRole);
        Directory.CreateDirectory(studioDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(studioDirectory, fileName);
        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);

        return $"/uploads/studio-media/{studioId:D}/{mediaRole}/{fileName}";
    }

    private static string ResolveRootPath(string configuredRootPath, string contentRootPath)
    {
        if (Path.IsPathRooted(configuredRootPath))
        {
            return configuredRootPath;
        }

        return Path.GetFullPath(Path.Combine(contentRootPath, configuredRootPath));
    }
}
