
using Itam.AssetService.Infrastructure.Contracts;
using Itam.AssetService.Infrastructure.Contracts.Entities;
using Itam.AssetService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Itam.AssetService.Infrastructure.Repositories
{
    public class CustodianRepository(AssetDbContext db) : ICustodianRepository
    {
        public async Task<Custodian?> GetByIdAsync(Guid id) => await db.Custodians.FirstOrDefaultAsync(custodian => custodian.Id == id && !custodian.IsDeleted); 
        
    }
}