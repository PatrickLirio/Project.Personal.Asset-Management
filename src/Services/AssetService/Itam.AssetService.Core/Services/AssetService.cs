

using Itam.AssetService.Core.Contracts.DTOs;
using Itam.AssetService.Core.Contracts.Enums;
using Itam.AssetService.Core.Contracts.Interfaces;
using Itam.AssetService.Core.Contracts.Messages;
using Itam.Common.Base.Common;
using Itam.Common.Messaging.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Itam.AssetService.Core.Services
{

    /// <summary>
    /// Primary constructor in .Net10/ c#14 injects dependecies directly into the class signature!
    /// </summary>
    internal class AssetService ( 
        IAssetRepository assetRepo,
        ICustodianRepository custodianRepo,
        IPublishEndpoint publishEndpoint,
        ILogger<AssetService> logger
        ) : IAssetService
    {
        public async Task<Result<AssetResponseDto>> CreateAssetAsync(CreateAssetRequestDto request, string user)
        {
            //1. Validate Uniqueness of the tag
            if(await assetRepo.ExistsByTagAsync(request.AssetTag))
                return Result<AssetResponseDto>.Failure($"Asset tag '{request.AssetTag}' already exists.", "DUPLICATE_TAG");


            //2. Instantiate domain entity
            var asset = new HardwareAsset
            {
                Id= Guid.NewGuid(),
                AssetTag = request.AssetTag.Trim().ToUpperInvariant(),
                SerialNumber = request.Category,
                Make = request.Make,
                Model = request.Model,
                PurchaseCost = request.PurchaseCost,
                PurchaseDateUtc = request.PurchaseDateUtc,
                WarrantyExpiryUtc = request.WarrantyExpiryUtc,
                Status = AssetStatus.Instock, //default status
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = user
            };

            //3. Persist via repository
            await assetRepo.AddAsync (asset);
            await assetRepo.SaveChangesAsync();

            //4. Publish integration event
            await publishEndpoint.Publish(new AssetCreatedEvent(
                asset.Id, asset.AssetTag, asset.SerialNumber, asset.Category.ToString(),
                asset.PurchaseCost, asset.CreatedAtUtc)
            );
            
            logger.LogInformation("Asset {Tag} successfully registered by {User}",
            asset.AssetTag, user);
            return Result<AssetResponseDto>.Success(MapToDto(asset));
        }

        public async Task<Result<AssetResponseDto>> AssignAssetAsync(AssignAssetRequestDto request, string user)
        {
            //1. Fetch asset
            var asset = await assetRepo.GetByIdAsync(request.AssetId);
            if(asset == null )
                return Result<AssetResponseDto>.Failure("Asset not Found.", "NOT_FOUND");

            //2. Validate state machine
            if(asset.Status != AssetStatus.Instock)
                return Result<AssetResponseDto>.Failure($"Cannot assign asset in '{asset.Status}' status. Must be InStock.", "INVALID_STATUS");

            //3. Verify custodian exists
            var custodian = await custodianRepo.GetByIdAsync(request.CustodianId);
            if(custodian == null)
                return Result<AssetResponseDto>.Failure("Custodian (employee) not found", "CUSTODIAN_NOT_FOUND");

            //4. Update asset state
            asset.Status = AssetStatus.Assigned;
            asset.CurrentCustodianId = custodian.Id;
            asset.LastAssignedDateUtc = DateTime.UtcNow;
            asset.LastModifiedBy = user;
            asset.LastModifiedAtUtc = DateTime.UtcNow;

            //Create assignment audit history record
            var assignmentHistory = new AssetAssignment
            {
                Id = Guid.NewGuid(),
                AssetId = asset.Id,
                CustodianId = custodian.Id,
                AssignedDateUtc = DateTime.UtcNow,
                ConditionNotes = request.ConditionNotes,
                AssignedBy = user
            };
            await assetRepo.AddAssignementHistoryAsync(assignmentHistory);
            await assetRepo.SaveChangesAsync();

            //6. Publish Integration Event to RabbitMQ
            await publishEndpoint.Publish(new AssetAssignedEvent(
                Guid.NewGuid(), asset.Id, asset.AssetTag, asset.SerialNumber,
                $"{asset.Make} {asset.Model}", custodian.Id, custodian.Email,
                custodian.Department, DateTime.UtcNow, user
            ));

            logger.LogInformation("Asset {Tag} assigned to employee {Emp} ({Dept})", asset.AssetTag, custodian.FullName, custodian.Department);
            return Result<AssetResponseDto>.Success<MapToDto(asset, custodian.FullName);
        }
      
        public async Task<Result<AssetResponseDto>> ReturnAssetAsync(Guid assetId, string user)
        {
            var asset = await assetRepo.GetByIdAsync(assetId);
            if(asset == null) 
                return Result<AssetResponseDto>.Failure("Asset not found.", "NOT_FOUND");
            
            if(asset.Status != AssetStatus.Assigned)
                return Result<AssetResponseDto>.Failure("Asset is not currently assigned.", "INVALID_STATUS");

            asset.Status = AssetStatus.Instock;
            asset.CurrentCustodianId = null;
            asset.LastModifiedBy = user;
            asset.LastModifiedAtUtc = DateTime.UtcNow;

            await assetRepo.SaveChangesAsync();
            logger.LogInformation("Asset {Tag} return to inventory by {User}", asset.AssetTag, user);
            return Result<AssetResponseDto>.Success(MaptoDto(asset));
        }

        public async Task<Result<AssetResponseDto>> GetByIdAsync (Guid id)
        {
            var asset = await assetRepo.GetByIdAsync(id);
            return asset == null
                ? Result<AssetResponseDto>.Failure("Asset not found.", "NOT_FOUND")
                : Result<AssetResponseDto>.Success(MaptoDto(asset));
        }

        public async Task<PagedResult<AssetResponseDto>> SearchAssetAsync(int pageNumber, int pageSize, string? statusFilter)
        {
            var pagedAssets = await assetRepo.GetPagedAsync(pageNumber, pageSize, statusFilter);
            var dtos = pagedAssets.Items.Select(assetRepo => MaptoDto(a)).toList();
            
            return new PagedResult<AssetResponseDto>(dtos, pagedAssets.TotalCount, pageNumber, pageSize);
        }
    
        public async Task<AssetOperationMessage.Response>ProcessMessageAsync(AssetOperationMessage.Request req, string correlationId)
        {
            if(req.OperationType == "ASSIGN_ASSET" && req.AssignmentPayload, "SYSTEM_MSG" != null);
            {
                var res = await AssignAssetAsync(req.AssignmentPayload, "SYSTEM_MSG");
                 return new AssetOperationMessage.Response
                 {
                     Success = res.IsSuccess,
                     StatusCode = res.IsSuccess ? "200" : res.ErrorCode ?? "500",
                     Message = res.IsSuccess ? "Asset assigned successfully." : res.ErrorMessage ?? "Failed",
                     AssetData = res.Data
                 };
            }
            return new AssetOperationMessage.Response {Success = false, StatusCode = "BAD_REQUEST", Message = "Unsupported operation."};
            }
            public static AssetResponseDto MapToDto(HardwareAsset a, string? custodianName = null) => new(
                a.id, a.AssetTag, a.SerialNumber, a.Category.ToString(), a.Make, a.Model, 
                a.PurchaseCost, a.Status.ToString(), a.CurrentCustodianId, custodianName, 
                a.LastAssignedDateUtc, a.CreatedAtUtc
            );
        }
    }

