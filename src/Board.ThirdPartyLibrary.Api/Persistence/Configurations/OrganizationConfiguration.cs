using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations", tableBuilder =>
            tableBuilder.HasComment("Developer organizations that own catalog content and related configuration."));

        builder.HasKey(organization => organization.Id)
            .HasName("pk_organizations");

        builder.Property(organization => organization.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(organization => organization.Slug)
            .HasColumnName("slug")
            .HasMaxLength(100)
            .IsRequired()
            .HasComment("Human-readable unique route key used for public organization pages.");

        builder.HasIndex(organization => organization.Slug)
            .IsUnique()
            .HasDatabaseName("ux_organizations_slug");

        builder.Property(organization => organization.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(organization => organization.Description)
            .HasColumnName("description")
            .HasMaxLength(4000);

        builder.Property(organization => organization.LogoUrl)
            .HasColumnName("logo_url")
            .HasMaxLength(2048);

        builder.Property(organization => organization.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(organization => organization.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}
