



using Itam.AssetService.Core.Entities;
using Itam.Common.Base.Common;

namespace Itam.AssetService.Application.Repositories
{
    /// <summary>
    ///  This Interface Repo declares Capability of the Application and then can be used in Infrastrucure.
    /// </summary>
    public interface IAssetRepository
    {
       Task<HardwareAsset?> GetByIdAsync(Guid id, CancellationToken ct); 
       Task<AssetAssignment?> GetActiveAssignmentAsync(Guid assetId, CancellationToken ct);
       Task<bool> ExistsByTagAsync(string tag, CancellationToken ct);
       Task<bool> ExistsBySerialNumberAsync(string serialNumber, CancellationToken ct);
       Task AddAsync(HardwareAsset asset, CancellationToken ct);
       Task AddAssignmentHistoryAsync(AssetAssignment assignment, CancellationToken ct);
       Task<PagedResult<HardwareAsset>> GetPagedAsync(int page, int size, string? status, CancellationToken ct);
       Task<PagedResult<HardwareAsset>> GetAssignedToUserAsync(Guid identityUserId, int page, int size, CancellationToken ct);
        Task<bool> IsAssignedToUserAsync(Guid assetId, Guid identityUserId, CancellationToken ct);
       Task SaveChangesAsync(CancellationToken ct);
    }
}