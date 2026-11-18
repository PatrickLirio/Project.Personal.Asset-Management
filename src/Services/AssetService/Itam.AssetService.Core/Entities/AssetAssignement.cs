
using Itam.Common.Base.Entities;

namespace Itam.AssetService.Core.Entities
{
       public sealed class AssetAssignment : AuditableEntity<Guid>
    {
        public Guid AssetId { get; set;}
        public HardwareAsset Asset { get; set;} = null;
        public Guid CustodianId { get; set;}
        public Custodian Custodian { get; set;} = null;
        public DateTime AssignedDateUtc { get; set;}
        public DateTime? ReturnedDateUtc { get; set;}
        public string? ConditionNotes { get; set;}
        public string AssignedBy { get; set;} = string.Empty;
        public string? ReturnedBy { get; set; }
    }

    public sealed class Custodian : AuditableEntity<Guid>
    {
        public string EmployeeNumber { get; set;} = string.Empty;
        public string FullName { get; set;} = string.Empty;
        public string Email { get; set;} = string.Empty;
        public string Department{ get; set;} = string.Empty;
        // Links an employee record to an Identity login. Null means not linked yet.
        public Guid? IdentityUserId { get; private set; }
        public ICollection<AssetAssignment> Assignments { get; set; } = new List<AssetAssignment>();

        public void LinkToUser(Guid identityUserId)
        {
            if (IdentityUserId.HasValue && IdentityUserId.Value != identityUserId)
                throw new InvalidOperationException("This Custodian is already linked to another login.");
            IdentityUserId = identityUserId;
        }
    }
   

}