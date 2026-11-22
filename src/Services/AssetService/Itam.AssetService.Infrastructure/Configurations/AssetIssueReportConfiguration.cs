

using Itam.AssetService.Core.Entities;
using Itam.AssetService.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itam.AssetService.Infrastructure.Configurations;

public sealed class AssetIssueReportConfiguration
    : IEntityTypeConfiguration<AssetIssueReport>
{
    public void Configure(EntityTypeBuilder<AssetIssueReport> builder)
    {
        builder.Property(issue => issue.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(issue => issue.ResolutionNote)
            .HasMaxLength(1000);

        builder.HasOne(issue => issue.Asset)
            .WithMany()
            .HasForeignKey(issue => issue.AssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AssetUser>()
            .WithMany()
            .HasForeignKey(issue => issue.ReportedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
