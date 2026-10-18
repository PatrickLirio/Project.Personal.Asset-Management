
using Itam.AssetService.Core.Contracts.Enums;
using Itam.Common.Base.Entities;

namespace Itam.AssetService.Infrastructure.Contracts.Entities
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
        public AssetStatus Status { get; set;} = AssetStatus.Instock;


        //Custodian Foreign Key
        public Guid? CurrentCustodianId { get; set;}
        public Custodian? CurrentCustodian { get; set;}
        public DateTime? LastAssignedDateUtc { get; set;}

        public ICollection<AssetAssignment> AssignmentHistory { get; set;} = new List<AssetAssignment>();      
    }

    public class AssetAssignment : AuditableEntity<Guid>
    {
        public Guid AssetId { get; set;}
        public HardwareAsset Asset { get; set;} = null;
        public Guid CustodianId { get; set;}
        public Custodian Custodian { get; set;} = null;
        public DateTime AssignedDateUtc { get; set;}
        public DateTime? ReturnedDateUtc { get; set;}
        public string? ConditionNotes { get; set;}
        public string AssignedBy { get; set;} = string.Empty;
    }

    public class Custodian : AuditableEntity<Guid>
    {
        public string EmployeeNumber { get; set;} = string.Empty;
        public string FullName { get; set;} = string.Empty;
        public string Email { get; set;} = string.Empty;
        public string Department{ get; set;} = string.Empty;
    }
}