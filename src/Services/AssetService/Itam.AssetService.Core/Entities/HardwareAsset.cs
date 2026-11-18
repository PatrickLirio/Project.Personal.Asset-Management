
using Itam.AssetService.Core.Entities;
using Itam.AssetService.Core.Enums;
using Itam.AssetService.Core.Errors;
using Itam.Common.Base.Entities;

namespace Itam.AssetService.Core.Entities
{
    public class HardwareAsset : AuditableEntity<Guid>
    {
        public string AssetTag { get; set;} = string.Empty;
        public string SerialNumber { get; set;} = string.Empty;
        public AssetCategory Category { get; set;} = AssetCategory.Laptop;
        public string Make { get; set;} = string.Empty;
        public string Model { get; set;} = string.Empty;
        public decimal PurchaseCost { get; set;}
        public DateTime PurchaseDateUtc { get; set;}
        public DateTime WarrantyExpiryUtc{ get; set;}
        public AssetStatus Status { get; set;} = AssetStatus.InStock;
       public string? RetirementReason { get; private set; }
        public DateTime? RetiredAtUtc { get; private set; }
        public Guid? RetiredByUserId { get; private set; }



        //Custodian Foreign Key
        public Guid? CurrentCustodianId { get; set;}
        public Custodian? CurrentCustodian { get; set;}
        public DateTime? LastAssignedDateUtc { get; set;}

        public ICollection<AssetAssignment> AssignmentHistory { get; set;} = new List<AssetAssignment>();      
    

    public void AssignTo(Guid custodianId)
    {
        if (Status != AssetStatus.InStock)
            throw new AssetRuleException("Only an in-stock asset can be assigned.");
 
        CurrentCustodianId = custodianId;
        LastAssignedDateUtc = DateTime.UtcNow;
        Status = AssetStatus.Assigned;
    }
 
    public void ReturnToStock()
    {
        if (Status != AssetStatus.Assigned)
            throw new AssetRuleException("Only an assigned asset can be returned.");
 
        CurrentCustodianId = null;
        Status = AssetStatus.InStock;
    }

    public void StartRepair()
    {
        if (Status is AssetStatus.InRepair or AssetStatus.Retired)
            throw new AssetRuleException("This asset cannot enter repair from its current state.");
 
        // Keep the custodian while repair is open so the owner can see the status.
        Status = AssetStatus.InRepair;
    }
 
    public void CompleteRepair()
    {
        if (Status != AssetStatus.InRepair)
            throw new AssetRuleException("Only an asset in repair can complete repair.");
 
        Status = CurrentCustodianId.HasValue ? AssetStatus.Assigned : AssetStatus.InStock;
    }
 
    public void TransferTo(Guid newCustodianId)
    {
        if (Status != AssetStatus.Assigned)
            throw new AssetRuleException("Only an assigned asset can be transferred.");
        if (newCustodianId == Guid.Empty || newCustodianId == CurrentCustodianId)
            throw new AssetRuleException("Choose a different valid Custodian for the transfer.");
 
        CurrentCustodianId = newCustodianId;
        LastAssignedDateUtc = DateTime.UtcNow;
    }
 
    public void Retire(Guid adminId, string reason)
    {
        if (Status == AssetStatus.Retired)
            throw new AssetRuleException("This asset is already retired.");
        if (Status is AssetStatus.Assigned or AssetStatus.InRepair)
            throw new AssetRuleException("Return the asset and close repair before retirement.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new AssetRuleException("A retirement reason is required.");
 
        Status = AssetStatus.Retired;
        RetirementReason = reason.Trim();
        RetiredAtUtc = DateTime.UtcNow;
        RetiredByUserId = adminId;
    }


}
}