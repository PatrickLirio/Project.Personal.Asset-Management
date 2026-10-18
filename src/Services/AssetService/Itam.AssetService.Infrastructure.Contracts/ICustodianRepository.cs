

using Itam.AssetService.Infrastructure.Contracts.Entities;

namespace Itam.AssetService.Infrastructure.Contracts
{
    public interface ICustodianRepository
    {
        Task<Custodian?> GetByIdAsync (Guid id);
    }
}