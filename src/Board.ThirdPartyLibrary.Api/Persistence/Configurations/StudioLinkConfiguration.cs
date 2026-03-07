using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

internal sealed class StudioLinkConfiguration : IEntityTypeConfiguration<StudioLink>
{
    public void Configure(EntityTypeBuilder<StudioLink> builder)
    {
        builder.ToTable("studio_links", tableBuilder =>
            tableBuilder.HasComment("Public links associated with studio profiles."));

        builder.HasKey(link => link.Id)
            .HasName("pk_studio_links");

        builder.Property(link => link.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(link => link.StudioId)
            .HasColumnName("studio_id")
            .IsRequired();

        builder.HasIndex(link => link.StudioId)
            .HasDatabaseName("ix_studio_links_studio_id");

        builder.Property(link => link.Label)
            .HasColumnName("label")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(link => link.Url)
            .HasColumnName("url")
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(link => link.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(link => link.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}
