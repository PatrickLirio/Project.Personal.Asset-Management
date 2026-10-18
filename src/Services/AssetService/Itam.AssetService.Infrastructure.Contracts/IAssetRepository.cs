
using Itam.AssetService.Infrastructure.Contracts.Entities;
using Itam.Common.Base.Common;

namespace Itam.AssetService.Infrastructure.Contracts
{
    public interface IAssetRepository
    {
       Task<HardwareAsset?> GetByIdAsync(Guid id); 
       Task<bool> ExistsByTagAsync(string tag);
       Task AddAsync(HardwareAsset asset);
       Task AddAssignmentHistoryAsync(AssetAssignment assignment);
       Task<PagedResult<HardwareAsset>> GetPagedAsync(int page, int size, string? status);
       Task SaveChangesAsync();
    }
}