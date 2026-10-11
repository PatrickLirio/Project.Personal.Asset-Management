
namespace Itam.AssetService.Core.Contracts.Enums
{
    public enum AssetStatus
    {
        Instock = 1, //Available in inventory for assignement
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
}
