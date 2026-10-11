
using Itam.AssetService.Core.Contracts.Enums;
using System.ComponentModel.DataAnnotations;

namespace Itam.AssetService.Core.Contracts.DTOs
{
    public record CreateAssetRequestDto(
        [Required, MaxLength(50)] string AssetTag,
        [Required, MaxLength(100)] string SerialNumber,
        [Required] AssetCategory Category,
        [Required, MaxLength(100)] string Make,
        [Required, MaxLength(100)] string Model,
        [Range(0, 50000)] decimal PurchaseCost,
        DateTime PurchaseDateUtc,
        DateTime WarrantyExpiryUtc
   );

    public record AssignAssetRequestDto(
        [Required] Guid AssetId,
        [Required] Guid CustodianId,
        [MaxLength(500)] string? ConditionNotes
        );

    public record AssetResponseDto(
        Guid Id,
        string AssetTag,
        string SerialNumber,
        string Category,
        string Make,
        string Model,
        decimal PurchaseCost,
        string status,
        Guid? CurrentCustodianId,
        string? CurrentCustodianName,
        DateTime? AssignedDateUtc,
        DateTime CreatedAtUtc
    );
}
