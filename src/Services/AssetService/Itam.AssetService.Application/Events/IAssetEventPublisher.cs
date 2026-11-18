

using Itam.AssetService.Core.Entities;

namespace Itam.AssetService.Application.Events
{
    public interface IAssetEventPublisher
    {
        Task AssetAssignedAsync(
            HardwareAsset asset,
            Custodian custodian,
            string actor, 
            CancellationToken ct
        );
    }
}