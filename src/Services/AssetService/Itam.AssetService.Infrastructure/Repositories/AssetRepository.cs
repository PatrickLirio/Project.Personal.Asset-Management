
using Itam.AssetService.Core.Contracts.Enums;
using Itam.AssetService.Infrastructure.Contracts;
using Itam.AssetService.Infrastructure.Contracts.Entities;
using Itam.AssetService.Infrastructure.Data;
using Itam.Common.Base.Common;
using Microsoft.EntityFrameworkCore;

namespace Itam.AssetService.Infrastructure.Repositories
{
    public class AssetRepository(AssetDbContext db) : IAssetRepository
    {
        public async Task<HardwareAsset?> GetByIdAsync(Guid id) => await db.HardwareAssets.Include(a => a.CurrentCustodian).FirstOrDefaultAsync(a => a.Id == id);
        public async Task<bool> ExistsByTagAsync(string tag) => await db.HardwareAssets.AnyAsync(a => a.AssetTag == tag);
        public async Task AddAsync(HardwareAsset asset) => await db.HardwareAssets.AddAsync(asset);
        public async Task AddAssignmentHistoryAsync(AssetAssignment assignment) => await db.AssetAssignments.AddAsync(assignment);
        public async Task<PagedResult<HardwareAsset>> GetPagedAsync(int page, int size, string? status)
        {
            var query = db.HardwareAssets.AsQueryable();
            if(!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AssetStatus>(status, true, out var st))
            query = query.Where(a => a.Status == st);
            
            var total = await query.CountAsync();
            var items = await query.OrderByDescending(a => a.CreatedAtUtc)
                        .Skip((page -1) * size)
                        .Take(size)
                        .ToListAsync();
            
            return new PagedResult<HardwareAsset>(items, total, page, size);
        }

        public async Task SaveChangesAsync() => await db.SaveChangesAsync();
    }
}