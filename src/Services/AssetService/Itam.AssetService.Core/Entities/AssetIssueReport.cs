

using Itam.AssetService.Core.Enums;
using Itam.AssetService.Core.Errors;
using Itam.Common.Base.Entities;

namespace Itam.AssetService.Core.Entities
{
    public sealed class AssetIssueReport : AuditableEntity<Guid>
{
    public Guid AssetId { get; set; }
    public HardwareAsset Asset { get; set; } = null!;
    public Guid ReportedByUserId { get; set; }
    public string Description { get; set; } = string.Empty;
    public IssueReportStatus Status { get; private set; } = IssueReportStatus.Open;
    public DateTime ReportedAtUtc { get; set; } = DateTime.UtcNow;
    public string? ResolutionNote { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
 
    public void StartRepair()
    {
        if (Status is not (IssueReportStatus.Open or IssueReportStatus.UnderReview))
            throw new AssetRuleException("This report cannot start repair from its current status.");
        Status = IssueReportStatus.Repairing;
    }
 
    public void Resolve(string note)
    {
        if (Status != IssueReportStatus.Repairing)
            throw new AssetRuleException("Only a report under repair can be resolved.");
        Status = IssueReportStatus.Resolved;
        ResolutionNote = note;
        ResolvedAtUtc = DateTime.UtcNow;
    }
}

}