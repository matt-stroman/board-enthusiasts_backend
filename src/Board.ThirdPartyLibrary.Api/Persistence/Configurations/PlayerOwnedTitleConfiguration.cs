using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

internal sealed class PlayerOwnedTitleConfiguration : IEntityTypeConfiguration<PlayerOwnedTitle>
{
    public void Configure(EntityTypeBuilder<PlayerOwnedTitle> builder)
    {
        builder.ToTable("player_owned_titles", tableBuilder =>
            tableBuilder.HasComment("Private owned-title library entries for players."));

        builder.HasKey(entry => new { entry.UserId, entry.TitleId })
            .HasName("pk_player_owned_titles");

        builder.Property(entry => entry.UserId)
            .HasColumnName("user_id");

        builder.Property(entry => entry.TitleId)
            .HasColumnName("title_id");

        builder.Property(entry => entry.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne(entry => entry.User)
            .WithMany(user => user.OwnedTitles)
            .HasForeignKey(entry => entry.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_player_owned_titles_users");

        builder.HasOne(entry => entry.Title)
            .WithMany(title => title.OwnedByUsers)
            .HasForeignKey(entry => entry.TitleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_player_owned_titles_titles");
    }
}
