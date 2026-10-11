

using Itam.AssetService.Core.Contracts.DTOs;
using Itam.AssetService.Core.Contracts.Messages;
using Itam.Common.Base.Common;
using MassTransit;

namespace Itam.AssetService.Core.Contracts.Interfaces
{
    public interface IAssetService
    {
        Task<Result<AssetResponseDto>> CreateAssetAsync(CreateAssetRequestDto request, string user);
        Task<Result<AssetResponseDto>> AssignAssetAsync(AssignAssetRequestDto request, string user);
        Task<Result<AssetResponseDto>> ReturnAssetAsync(Guid assetId, string user);
        Task<Result<AssetResponseDto>> GetByIdAsync(Guid id);
        Task<PagedResult<AssetResponseDto>> SearchAssetAsync(int pageNumber, int pageSize, string? statusFilter);
        Task<AssetOperationMessage.Response> ProcessMessageAsync(AssetOperationMessage.Request request, string correlationId);
    }
}
