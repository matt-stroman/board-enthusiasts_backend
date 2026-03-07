using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

internal sealed class TitleReportMessageConfiguration : IEntityTypeConfiguration<TitleReportMessage>
{
    public void Configure(EntityTypeBuilder<TitleReportMessage> builder)
    {
        builder.ToTable("title_report_messages", tableBuilder =>
            tableBuilder.HasComment("Thread messages exchanged during title-report review."));

        builder.HasKey(message => message.Id)
            .HasName("pk_title_report_messages");

        builder.Property(message => message.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(message => message.TitleReportId)
            .HasColumnName("title_report_id")
            .IsRequired();

        builder.Property(message => message.AuthorUserId)
            .HasColumnName("author_user_id")
            .IsRequired();

        builder.Property(message => message.AuthorRole)
            .HasColumnName("author_role")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(message => message.Message)
            .HasColumnName("message")
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(message => message.Audience)
            .HasColumnName("audience")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(message => message.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(message => new { message.TitleReportId, message.CreatedAtUtc })
            .HasDatabaseName("ix_title_report_messages_report_created_at");

        builder.HasOne(message => message.TitleReport)
            .WithMany(report => report.Messages)
            .HasForeignKey(message => message.TitleReportId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_title_report_messages_reports");

        builder.HasOne(message => message.AuthorUser)
            .WithMany(user => user.TitleReportMessages)
            .HasForeignKey(message => message.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_title_report_messages_users");
    }
}
