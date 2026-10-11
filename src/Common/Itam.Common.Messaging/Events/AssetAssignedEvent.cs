

namespace Itam.Common.Messaging.Events
{
    /// <summary>
    /// Integration Event published when an asset is assigned to an employee.
    /// </summary>
    public record AssetAssignedEvent(
        Guid EventId,
        Guid AssetId,
        string AssetTag,
        string SerialNumber,
        string ModelName,
        Guid CustodianId,
        string EmployeeEmail,
        string Department,
        DateTime AssignedDateUtc,
        string AssignedBy
        );
    public record AssetCreatedEvent(
        Guid AssetId,
        string AssetTag,
        string SerialNumber,
        string Category,
        decimal PurchaseCost,
        DateTime CreatedDateUtc
        );
}
