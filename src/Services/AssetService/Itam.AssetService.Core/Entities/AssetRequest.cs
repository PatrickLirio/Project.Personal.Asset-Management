
using Itam.AssetService.Core.Enums;
using Itam.AssetService.Core.Errors;
using Itam.Common.Base.Entities;

namespace Itam.AssetService.Core.Entities
{
public sealed class AssetRequest : AuditableEntity<Guid>
{
    public Guid RequestedByUserId { get; private set; }
    public AssetCategory RequestedCategory { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public AssetRequestStatus Status { get; private set; } = AssetRequestStatus.Pending;
    public Guid? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public string? DecisionNote { get; private set; }
    public Guid? FulfilledAssignmentId { get; private set; }
 
    private AssetRequest() { } // EF Core uses this when it reads a saved row.
 
    public static AssetRequest Submit(Guid userId, AssetCategory category, string reason)
    {
        if (userId == Guid.Empty)
            throw new AssetRuleException("A signed-in user is required to request an asset.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new AssetRuleException("A reason is required for an asset request.");
 
        return new AssetRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = userId,
            RequestedCategory = category,
            Reason = reason.Trim()
        };
    }
 
    public void Approve(Guid adminId, string? note)
    {
        EnsurePending();
        Status = AssetRequestStatus.Approved;
        ReviewedByUserId = adminId;
        ReviewedAtUtc = DateTime.UtcNow;
        DecisionNote = note;
    }
 
    public void Reject(Guid adminId, string note)
    {
        EnsurePending();
        Status = AssetRequestStatus.Rejected;
        ReviewedByUserId = adminId;
        ReviewedAtUtc = DateTime.UtcNow;
        DecisionNote = note;
    }
 
    public void MarkFulfilled(Guid assignmentId)
    {
        if (Status != AssetRequestStatus.Approved)
            throw new AssetRuleException("Only an approved request can be fulfilled.");
        if (FulfilledAssignmentId.HasValue)
            throw new AssetRuleException("This request has already been fulfilled.");
        FulfilledAssignmentId = assignmentId;
    }
 
    private void EnsurePending()
    {
        if (Status != AssetRequestStatus.Pending)
            throw new AssetRuleException("Only a pending request can be reviewed.");
    }
}

}