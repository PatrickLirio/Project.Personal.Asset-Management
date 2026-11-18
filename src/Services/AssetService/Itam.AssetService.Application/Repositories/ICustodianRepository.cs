

using Itam.AssetService.Core.Entities;


namespace Itam.AssetService.Application.Repositories
{
    public interface ICustodianRepository
    {
        Task<Custodian?> GetByIdAsync(Guid id, CancellationToken ct);
        Task<Custodian?> GetByIdentityUserIdAsync(Guid userId, CancellationToken ct);
        Task SaveChangesAsync(CancellationToken ct);

    }
}