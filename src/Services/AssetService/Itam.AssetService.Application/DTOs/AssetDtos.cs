

using System.ComponentModel.DataAnnotations;
using Itam.AssetService.Core.Entities;
using Itam.AssetService.Core.Enums;

namespace Itam.AssetService.Application.Dtos;

    public sealed record CreateAssetRequestDto(
        [Required, MaxLength(50)] string AssetTag,
        [Required, MaxLength(100)] string SerialNumber,
        [Required] AssetCategory Category,
        [Required, MaxLength(100)] string Make,
        [Required, MaxLength(100)] string Model,
        [Range(0, 50000)] decimal PurchaseCost,
        DateTime PurchaseDateUtc,
        DateTime WarrantyExpiryUtc
   );

    public sealed record AssignAssetRequestDto(
        [Required] Guid AssetId,
        [Required] Guid CustodianId,
        [MaxLength(500)] string? ConditionNotes
        );


    public sealed record UpdateAssetRequestDto(
        [Required, MaxLength(50)] string AssetTag,
        [Required, MaxLength(100)] string SerialNumber,
        AssetCategory Category,
        [Required, MaxLength(100)] string Make,
        [Required, MaxLength(100)] string Model,
        [Range(0, 50000)] decimal PurchaseCost,
        DateTime PurchaseDateUtc, 
        DateTime WarrantyExpiryUtc
    );

    public sealed record AssetResponseDto(
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
    )
    {
    public static AssetResponseDto From(HardwareAsset asset, Custodian? custodian = null) => new(
        asset.Id, 
        asset.AssetTag, 
        asset.SerialNumber, 
        asset.Category.ToString(),
        asset.Make, 
        asset.Model, 
        asset.PurchaseCost, 
        asset.Status.ToString(),
        asset.CurrentCustodianId, 
        custodian?.FullName,
        asset.LastAssignedDateUtc, 
        asset.CreatedAtUtc
    );
    }


