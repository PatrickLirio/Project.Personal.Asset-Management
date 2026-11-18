

using Itam.AssetService.Core.Enums;
using Itam.AssetService.Core.Errors;
using Itam.Common.Base.Entities;

namespace Itam.AssetService.Core.Entities
{
    public sealed class AssetRepair : AuditableEntity<Guid>
{
    public Guid AssetId { get; set; }
    public Guid? IssueReportId { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public Guid StartedByUserId { get; set; }
    public string? ResolutionNote { get; set; }
    public AssetRepairStatus Status { get; private set; } = AssetRepairStatus.Open;
 
    public void Complete(string note)
    {
        if (Status != AssetRepairStatus.Open)
            throw new AssetRuleException("Only an open repair can be completed.");
        if (string.IsNullOrWhiteSpace(note))
            throw new AssetRuleException("A resolution note is required.");
 
        ResolutionNote = note.Trim();
        CompletedAtUtc = DateTime.UtcNow;
        Status = AssetRepairStatus.Completed;
    }
}

}