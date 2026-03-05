using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("users", tableBuilder =>
            tableBuilder.HasComment("Application-owned identity projection linked to a Keycloak subject."));

        builder.HasKey(user => user.Id)
            .HasName("pk_users");

        builder.Property(user => user.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(user => user.KeycloakSubject)
            .HasColumnName("keycloak_subject")
            .HasMaxLength(200)
            .IsRequired()
            .HasComment("Immutable Keycloak subject identifier used as the durable external identity link.");

        builder.HasIndex(user => user.KeycloakSubject)
            .IsUnique()
            .HasDatabaseName("ux_users_keycloak_subject");

        builder.Property(user => user.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200);

        builder.Property(user => user.UserName)
            .HasColumnName("user_name")
            .HasMaxLength(100);

        builder.Property(user => user.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(100);

        builder.Property(user => user.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(100);

        builder.Property(user => user.Email)
            .HasColumnName("email")
            .HasMaxLength(320);

        builder.Property(user => user.EmailVerified)
            .HasColumnName("email_verified")
            .IsRequired();

        builder.Property(user => user.IdentityProvider)
            .HasColumnName("identity_provider")
            .HasMaxLength(100);

        builder.Property(user => user.AvatarUrl)
            .HasColumnName("avatar_url")
            .HasMaxLength(2048);

        builder.Property(user => user.AvatarImageContentType)
            .HasColumnName("avatar_image_content_type")
            .HasMaxLength(100);

        builder.Property(user => user.AvatarImageData)
            .HasColumnName("avatar_image_data")
            .HasColumnType("bytea");

        builder.Property(user => user.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(user => user.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}
