namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

internal sealed class Title
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string ContentKind { get; set; } = string.Empty;

    public string LifecycleStatus { get; set; } = string.Empty;

    public string Visibility { get; set; } = string.Empty;

    public Guid? CurrentMetadataVersionId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Organization Organization { get; set; } = null!;

    public TitleMetadataVersion? CurrentMetadataVersion { get; set; }

    public ICollection<TitleMetadataVersion> MetadataVersions { get; set; } = [];
}
