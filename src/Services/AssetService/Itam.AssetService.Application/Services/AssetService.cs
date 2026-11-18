

using Itam.AssetService.Application.Dtos;
using Itam.AssetService.Application.Events;
using Itam.AssetService.Application.Identity;
using Itam.AssetService.Application.Repositories;
using Itam.AssetService.Core.Entities;
using Itam.AssetService.Core.Enums;
using Itam.AssetService.Core.Errors;
using Itam.Common.Base.Common;
using Microsoft.Extensions.Logging;

namespace Itam.AssetService.Application.Services
{

    /// <summary>
    /// Primary constructor in .Net10/ c#14 injects dependecies directly into the class signature!
    /// </summary>
    public class AssetService ( 
        IAssetRepository assets,
        ICustodianRepository custodians,
        ICurrentUser currentUser,
        IAssetEventPublisher events,
        ILogger<AssetService> logger
        ) : IAssetService
    {
        public async Task<Result<AssetResponseDto>> CreateAssetAsync(CreateAssetRequestDto request, string actor, CancellationToken ct)
            {
                if (!currentUser.IsAdmin)
                    return Result<AssetResponseDto>.Failure("Admin role is required.", "FORBIDDEN");
                if (await assets.ExistsByTagAsync(request.AssetTag.Trim(), ct) ||
                    await assets.ExistsBySerialNumberAsync(request.SerialNumber.Trim(), ct)
                    )
                    return Result<AssetResponseDto>.Failure("Asset tag or serial number already exists.", "DUPLICATE_ASSET_IDENTIFIER");
            
                var asset = new HardwareAsset
                {
                    Id = Guid.NewGuid(), 
                    AssetTag = request.AssetTag.Trim(),
                    SerialNumber = request.SerialNumber.Trim(), 
                    Category = request.Category,
                    Make = request.Make.Trim(),
                    Model = request.Model.Trim(),
                    PurchaseCost = request.PurchaseCost,
                    PurchaseDateUtc = request.PurchaseDateUtc,
                    WarrantyExpiryUtc = request.WarrantyExpiryUtc
                };
                await assets.AddAsync(asset, ct);
                await assets.SaveChangesAsync(ct);
                return Result<AssetResponseDto>.Success(AssetResponseDto.From(asset));
            }
        public async Task<Result<AssetResponseDto>> UpdateAssetAsync(Guid assetId, UpdateAssetRequestDto request, CancellationToken ct)
        {
            if (!currentUser.IsAdmin)
                return Result<AssetResponseDto>.Failure("Admin role is required.", "FORBIDDEN");
            var asset = await assets.GetByIdAsync(assetId, ct);
            if (asset is null)
                return Result<AssetResponseDto>.Failure("Asset not found.", "NOT_FOUND");
        
            var tagChanged = !string.Equals(asset.AssetTag, request.AssetTag.Trim(), StringComparison.OrdinalIgnoreCase);
            if (tagChanged && await assets.ExistsByTagAsync(request.AssetTag.Trim(), ct))
                return Result<AssetResponseDto>.Failure("Asset tag already exists.", "DUPLICATE_TAG");
            var serialChanged = !string.Equals(asset.SerialNumber, request.SerialNumber.Trim(), StringComparison.OrdinalIgnoreCase);
            if (serialChanged && await assets.ExistsBySerialNumberAsync(request.SerialNumber.Trim(), ct))
                return Result<AssetResponseDto>.Failure("Serial number already exists.", "DUPLICATE_SERIAL_NUMBER");
        
            asset.AssetTag = request.AssetTag.Trim();
            asset.SerialNumber = request.SerialNumber.Trim();
            asset.Category = request.Category;
            asset.Make = request.Make.Trim();
            asset.Model = request.Model.Trim();
            asset.PurchaseCost = request.PurchaseCost;
            asset.PurchaseDateUtc = request.PurchaseDateUtc;
            asset.WarrantyExpiryUtc = request.WarrantyExpiryUtc;
            await assets.SaveChangesAsync(ct);
            return Result<AssetResponseDto>.Success(AssetResponseDto.From(asset, asset.CurrentCustodian));
        }
        public async Task<Result<AssetResponseDto>> AssignAssetAsync(AssignAssetRequestDto request, string actor, CancellationToken ct)
        {
            //validate
            if(!currentUser.IsAdmin)
                return Result<AssetResponseDto>.Failure("Admin role is required.", "FORBIDDEN");
            
            var asset = await assets.GetByIdAsync(request.AssetId, ct);
            if(asset is null)
                return Result<AssetResponseDto>.Failure("Asset not found.", "NOT_FOUND");

            var custodian = await custodians.GetByIdAsync(request.CustodianId, ct);
            if (custodians is null)
                return Result<AssetResponseDto>.Failure("Custodian not found.", "CUSTODIAN_NOT_FOUND");
            
            //call domain method
            try
            {
                asset.AssignTo(custodian.Id);
            }
            catch (AssetRuleException exception)
            {
                return Result<AssetResponseDto>.Failure(exception.Message, "INVALID_STATUS");
            }
    
            var assignment = new AssetAssignment
            {
                Id = Guid.NewGuid(),
                AssetId = asset.Id,
                CustodianId = custodian.Id,
                AssignedDateUtc = DateTime.UtcNow,
                AssignedBy = actor
            };

            //save
            await assets.AddAssignmentHistoryAsync(assignment, ct);
            await assets.SaveChangesAsync(ct);
    
            await events.AssetAssignedAsync(asset, custodian, actor, ct);
            logger.LogInformation("Asset {AssetTag} assigned by {Actor}", asset.AssetTag, actor);
            
            //return dto
            return Result<AssetResponseDto>.Success(AssetResponseDto.From(asset, custodian));

        }
      
        public async Task<Result<AssetResponseDto>> ReturnAssetAsync(Guid assetId, string actor, CancellationToken ct)
        {
            if (!currentUser.IsAdmin)
                return Result<AssetResponseDto>.Failure("Admin role is required.", "FORBIDDEN");
            var asset = await assets.GetByIdAsync(assetId, ct);
            var active = await assets.GetActiveAssignmentAsync(assetId, ct);
            if (asset is null || active is null)
                return Result<AssetResponseDto>.Failure("Active assignment not found.", "NOT_FOUND");
        
            try
            {
                asset.ReturnToStock();
                active.ReturnedDateUtc = DateTime.UtcNow;
                active.ReturnedBy = actor;
                await assets.SaveChangesAsync(ct);
                return Result<AssetResponseDto>.Success(AssetResponseDto.From(asset));
            }
            catch (AssetRuleException ex)
            {
                return Result<AssetResponseDto>.Failure(ex.Message, "INVALID_ASSET_STATE");
            }
        }

        public async Task<Result<AssetResponseDto>> TransferAsync(Guid assetId, Guid newCustodianId, string note, CancellationToken ct)
        {
            if (!currentUser.IsAdmin || currentUser.UserId is not Guid adminId)
                return Result<AssetResponseDto>.Failure("Admin role is required.", "FORBIDDEN");

            var asset = await assets.GetByIdAsync(assetId, ct);
            if (asset is null)
                return Result<AssetResponseDto>.Failure("Asset not found.", "NOT_FOUND");

            var nextCustodian = await custodians.GetByIdAsync(newCustodianId, ct);
            if (nextCustodian is null)
                return Result<AssetResponseDto>.Failure("Custodian not found.", "CUSTODIAN_NOT_FOUND");

            var activeAssignment = await assets.GetActiveAssignmentAsync(assetId, ct);
            if (activeAssignment is null)
                return Result<AssetResponseDto>.Failure("Active assignment not found.", "ACTIVE_ASSIGNMENT_NOT_FOUND");

            if (asset.CurrentCustodianId != activeAssignment.CustodianId)
                return Result<AssetResponseDto>.Failure(
                    "The asset's current Custodian does not match its active assignment.",
                    "ASSIGNMENT_MISMATCH");

            try
            {
                var now = DateTime.UtcNow;

                // Core checks that the asset is assigned and that the new Custodian differs.
                asset.TransferTo(nextCustodian.Id);

                // Close the previous assignment and create the new history row.
                activeAssignment.ReturnedDateUtc = now;
                activeAssignment.ReturnedBy = adminId.ToString();

                var newAssignment = new AssetAssignment
                {
                    Id = Guid.NewGuid(),
                    AssetId = asset.Id,
                    CustodianId = nextCustodian.Id,
                    AssignedDateUtc = now,
                    AssignedBy = adminId.ToString(),
                    ConditionNotes = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
                };

                await assets.AddAssignmentHistoryAsync(newAssignment, ct);
                await assets.SaveChangesAsync(ct);

                return Result<AssetResponseDto>.Success(
                    AssetResponseDto.From(asset, nextCustodian));
            }
            catch (AssetRuleException exception)
            {
                return Result<AssetResponseDto>.Failure(exception.Message, "INVALID_ASSET_STATE");
            }
        }

        public async Task<Result<bool>> RetireAsync(Guid assetId, string reason, CancellationToken ct)
        {
            if (!currentUser.IsAdmin || currentUser.UserId is not Guid adminId)
                return Result<bool>.Failure("Admin role is required.", "FORBIDDEN");

            var asset = await assets.GetByIdAsync(assetId, ct);
            if (asset is null)
                return Result<bool>.Failure("Asset not found.", "NOT_FOUND");

            // Guard against inconsistent data as well as the Core status rule.
            var activeAssignment = await assets.GetActiveAssignmentAsync(assetId, ct);
            if (activeAssignment is not null)
                return Result<bool>.Failure("Return or transfer the asset before retirement.", "ACTIVE_ASSIGNMENT");

            try
            {
                asset.Retire(adminId, reason);
                await assets.SaveChangesAsync(ct);
                return Result<bool>.Success(true);
            }
            catch (AssetRuleException exception)
            {
                return Result<bool>.Failure(exception.Message, "INVALID_ASSET_STATE");
            }
        }

        public async Task<Result<AssetResponseDto>> GetByIdAsync(Guid assetId, CancellationToken ct)
        {
            var asset = await assets.GetByIdAsync(assetId, ct);
            if (asset is null)
                return Result<AssetResponseDto>.Failure("Asset not found.", "NOT_FOUND");
        
            var isOwner = asset.CurrentCustodian?.IdentityUserId == currentUser.UserId;
            if (!currentUser.IsAdmin && !isOwner)
                return Result<AssetResponseDto>.Failure("Asset not found.", "NOT_FOUND");
        
            return Result<AssetResponseDto>.Success(
                AssetResponseDto.From(asset, asset.CurrentCustodian));
        }

        public async Task<PagedResult<AssetResponseDto>> GetMyAssetsAsync(int page, int size, CancellationToken ct)
        {
            if (currentUser.UserId is not Guid userId)
                return new PagedResult<AssetResponseDto>(Array.Empty<AssetResponseDto>(), 0, page, size);
        
            var result = await assets.GetAssignedToUserAsync(userId, page, size, ct);
            var items = result.Items
                .Select(asset => AssetResponseDto.From(asset, asset.CurrentCustodian))
                .ToList();
            return new PagedResult<AssetResponseDto>(items, result.TotalCount, page, size);
        }

        public async Task<PagedResult<AssetResponseDto>> SearchAllAsync(int page, int size, string? status, CancellationToken ct)
        {
            if (!currentUser.IsAdmin)
                return new PagedResult<AssetResponseDto>(Array.Empty<AssetResponseDto>(), 0, page, size);
        
            var result = await assets.GetPagedAsync(page, size, status, ct);
            var items = result.Items
                .Select(asset => AssetResponseDto.From(asset, asset.CurrentCustodian))
                .ToList();
            return new PagedResult<AssetResponseDto>(items, result.TotalCount, page, size);
        }

    }
}

