
using Itam.AssetService.Application.Dtos;
using Itam.Common.Base.Common;
using MassTransit;

namespace Itam.AssetService.Application.Services
{
    public interface IAssetService
    {
        Task<Result<AssetResponseDto>> CreateAssetAsync(CreateAssetRequestDto request, string actor, CancellationToken ct);
        Task<Result<AssetResponseDto>> UpdateAssetAsync(Guid assetId, UpdateAssetRequestDto request, CancellationToken ct);
        Task<Result<AssetResponseDto>> AssignAssetAsync(AssignAssetRequestDto request, string actor, CancellationToken ct);
        Task<Result<AssetResponseDto>> ReturnAssetAsync(Guid assetId, string actor, CancellationToken ct);
        Task<Result<AssetResponseDto>> TransferAsync(Guid assetId, Guid newCustodianId, string note, CancellationToken ct);
        Task<Result<bool>> RetireAsync(Guid assetId, string reason, CancellationToken ct);
        Task<Result<AssetResponseDto>> GetByIdAsync(Guid id,  CancellationToken ct);
        Task<PagedResult<AssetResponseDto>> GetMyAssetsAsync(int page, int size, CancellationToken ct);
        Task<PagedResult<AssetResponseDto>> SearchAllAsync(int page, int size, string? status, CancellationToken ct);
      //  Task<AssetOperationMessage.Response> ProcessMessageAsync(AssetOperationMessage.Request request, string correlationId);
    }
}
