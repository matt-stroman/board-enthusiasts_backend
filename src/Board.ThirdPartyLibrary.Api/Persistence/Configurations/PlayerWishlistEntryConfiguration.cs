using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

internal sealed class PlayerWishlistEntryConfiguration : IEntityTypeConfiguration<PlayerWishlistEntry>
{
    public void Configure(EntityTypeBuilder<PlayerWishlistEntry> builder)
    {
        builder.ToTable("player_wishlist_entries", tableBuilder =>
            tableBuilder.HasComment("Private wishlist entries for players."));

        builder.HasKey(entry => new { entry.UserId, entry.TitleId })
            .HasName("pk_player_wishlist_entries");

        builder.Property(entry => entry.UserId)
            .HasColumnName("user_id");

        builder.Property(entry => entry.TitleId)
            .HasColumnName("title_id");

        builder.Property(entry => entry.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne(entry => entry.User)
            .WithMany(user => user.WishlistEntries)
            .HasForeignKey(entry => entry.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_player_wishlist_entries_users");

        builder.HasOne(entry => entry.Title)
            .WithMany(title => title.WishlistedByUsers)
            .HasForeignKey(entry => entry.TitleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_player_wishlist_entries_titles");
    }
}
