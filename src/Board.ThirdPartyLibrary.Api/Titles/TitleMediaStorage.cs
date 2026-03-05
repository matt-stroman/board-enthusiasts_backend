using Microsoft.Extensions.Options;

namespace Board.ThirdPartyLibrary.Api.Titles;

/// <summary>
/// Persists uploaded title media files and returns backend-hosted route paths.
/// </summary>
internal interface ITitleMediaStorage
{
    /// <summary>
    /// Gets the absolute local filesystem root used for stored media files.
    /// </summary>
    string RootPath { get; }

    /// <summary>
    /// Saves uploaded title media content and returns a route path served by this API.
    /// </summary>
    Task<string> SaveTitleMediaAsync(
        Guid titleId,
        string mediaRole,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for local title media file storage.
/// </summary>
internal sealed class TitleMediaStorageOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "TitleMediaStorage";

    /// <summary>
    /// Default relative filesystem root for persisted title media.
    /// </summary>
    public const string DefaultRootPath = "artifacts/title-media";

    /// <summary>
    /// Gets or sets the local filesystem root path.
    /// </summary>
    public string RootPath { get; set; } = DefaultRootPath;
}

/// <summary>
/// Local filesystem-backed implementation of <see cref="ITitleMediaStorage" />.
/// </summary>
internal sealed class LocalTitleMediaStorage(
    IOptions<TitleMediaStorageOptions> options,
    IWebHostEnvironment environment) : ITitleMediaStorage
{
    /// <inheritdoc />
    public string RootPath { get; } = ResolveRootPath(options.Value.RootPath, environment.ContentRootPath);

    /// <inheritdoc />
    public async Task<string> SaveTitleMediaAsync(
        Guid titleId,
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
            _ => ".bin"
        };

        var titleDirectory = Path.Combine(RootPath, titleId.ToString("D"), mediaRole);
        Directory.CreateDirectory(titleDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(titleDirectory, fileName);
        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);

        return $"/uploads/title-media/{titleId:D}/{mediaRole}/{fileName}";
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
