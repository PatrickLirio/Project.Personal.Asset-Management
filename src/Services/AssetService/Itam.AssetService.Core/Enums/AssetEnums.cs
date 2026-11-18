
namespace Itam.AssetService.Core.Enums
{
    public enum AssetStatus
    {
        InStock = 1, //Available in inventory for assignement
        Assigned = 2, // Currently in use by an employee
        InRepair = 3, // Under maintenance or vendor RMA
        Retired = 4, // Decommisioned and no longer in use
    }

    public enum AssetCategory
    {
        Laptop = 1,
        Desktop = 2,
        Monitor = 3,
        MobileDevice = 4,
        NetworkEquipment = 5,
    }
    public enum AssetRequestStatus
    {
        Pending = 1,
        Approved = 2,
        Rejected = 3,
        Cancelled = 4
    }
    public enum IssueReportStatus
    {
        Open = 1,
        UnderReview = 2,
        Repairing = 3,
        Resolved = 4,
        Rejected = 5
    }
    public enum AssetRepairStatus
    {
        Open = 1,
        Completed = 2
    }
}
