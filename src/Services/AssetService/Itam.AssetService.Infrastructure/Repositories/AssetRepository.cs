

using Itam.AssetService.Application.Repositories;
using Itam.AssetService.Core.Entities;
using Itam.AssetService.Core.Enums;
using Itam.AssetService.Infrastructure.Data;
using Itam.Common.Base.Common;
using Microsoft.EntityFrameworkCore;

namespace Itam.AssetService.Infrastructure.Repositories
{
    public sealed class AssetRepository(AssetDbContext db) : IAssetRepository
    {
        public Task<HardwareAsset?> GetByIdAsync(Guid id, CancellationToken ct) => db.HardwareAssets.Include(x => x.CurrentCustodian).FirstOrDefaultAsync(x => x.Id == id, ct);
        public Task<AssetAssignment?> GetActiveAssignmentAsync(Guid assetId, CancellationToken ct) => db.AssetAssignments.FirstOrDefaultAsync(x => x.AssetId == assetId && x.ReturnedDateUtc == null,ct);
        public Task<bool> ExistsByTagAsync(string tag, CancellationToken ct) =>  db.HardwareAssets.AnyAsync(a => a.AssetTag == tag, ct);
        public Task<bool> ExistsBySerialNumberAsync(string serialNumber, CancellationToken ct) => db.HardwareAssets.AnyAsync(x => x.SerialNumber == serialNumber, ct);
        public async Task<PagedResult<HardwareAsset>> GetPagedAsync(int page, int size, string? status, CancellationToken ct)
        {
            var query = db.HardwareAssets.Include(x => x.CurrentCustodian).AsQueryable();
            if(!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AssetStatus>(status, ignoreCase: true, out var parsedStatus))
            query = query.Where(a => a.Status == parsedStatus);
            
            var total = await query.CountAsync(ct);
            var rows = await query
                        .OrderByDescending(x => x.CreatedAtUtc)
                        .Skip((page -1) * size)
                        .Take(size)
                        .ToListAsync(ct);
            
            return new PagedResult<HardwareAsset>(rows, total, page, size);
        }
        public async Task<PagedResult<HardwareAsset>> GetAssignedToUserAsync(Guid userId, int page, int size, CancellationToken ct)
        {
            var query = db.AssetAssignments
                .Where(x => x.ReturnedDateUtc == null && x.Custodian.IdentityUserId == userId);
            var total = await query.CountAsync(ct);
            var assignments = await query
                .AsNoTracking()
                .Include(x => x.Asset)
                    .ThenInclude(x => x.CurrentCustodian)
                .OrderByDescending(x => x.AssignedDateUtc)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(ct);
            var assets = assignments.Select(x => x.Asset).ToList();
    
            return new PagedResult<HardwareAsset>(assets, total, page, size);
        }   
        public Task<bool> IsAssignedToUserAsync(Guid assetId, Guid userId, CancellationToken ct) =>
            db.AssetAssignments.AnyAsync(x =>
                x.AssetId == assetId && x.ReturnedDateUtc == null &&
                x.Custodian.IdentityUserId == userId, ct);
    
        public async Task AddAsync(HardwareAsset asset, CancellationToken ct) =>
            await db.HardwareAssets.AddAsync(asset, ct);
    
        public async Task AddAssignmentHistoryAsync(AssetAssignment row, CancellationToken ct) =>
            await db.AssetAssignments.AddAsync(row, ct);
    
        public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    }
}