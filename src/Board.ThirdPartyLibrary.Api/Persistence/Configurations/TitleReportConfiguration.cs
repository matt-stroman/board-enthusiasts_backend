using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

internal sealed class TitleReportConfiguration : IEntityTypeConfiguration<TitleReport>
{
    public void Configure(EntityTypeBuilder<TitleReport> builder)
    {
        builder.ToTable("title_reports", tableBuilder =>
            tableBuilder.HasComment("Player-submitted moderation reports for titles."));

        builder.HasKey(report => report.Id)
            .HasName("pk_title_reports");

        builder.Property(report => report.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(report => report.TitleId)
            .HasColumnName("title_id")
            .IsRequired();

        builder.Property(report => report.ReporterUserId)
            .HasColumnName("reporter_user_id")
            .IsRequired();

        builder.Property(report => report.Status)
            .HasColumnName("status")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(report => report.Reason)
            .HasColumnName("reason")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(report => report.ResolutionNote)
            .HasColumnName("resolution_note")
            .HasMaxLength(2000);

        builder.Property(report => report.ResolvedByUserId)
            .HasColumnName("resolved_by_user_id");

        builder.Property(report => report.ResolvedAtUtc)
            .HasColumnName("resolved_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(report => report.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(report => report.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(report => new { report.TitleId, report.Status })
            .HasDatabaseName("ix_title_reports_title_status");

        builder.HasIndex(report => new { report.ReporterUserId, report.TitleId, report.Status })
            .HasDatabaseName("ix_title_reports_reporter_title_status");

        builder.HasOne(report => report.Title)
            .WithMany(title => title.Reports)
            .HasForeignKey(report => report.TitleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_title_reports_titles");

        builder.HasOne(report => report.ReporterUser)
            .WithMany(user => user.SubmittedTitleReports)
            .HasForeignKey(report => report.ReporterUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_title_reports_reporter_users");

        builder.HasOne(report => report.ResolvedByUser)
            .WithMany(user => user.ResolvedTitleReports)
            .HasForeignKey(report => report.ResolvedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_title_reports_resolved_by_users");
    }
}
