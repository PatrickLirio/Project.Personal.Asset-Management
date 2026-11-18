using Itam.AssetService.Application.Events;
using Itam.AssetService.Core.Entities;
using Itam.Common.Messaging.Events;
using MassTransit;

namespace Itam.AssetService.Infrastructure.Messaging;

public sealed class MassTransitAssetEventPublisher(IPublishEndpoint publishEndpoint)
    : IAssetEventPublisher
{
    public Task AssetAssignedAsync(
        HardwareAsset asset,
        Custodian custodian,
        string actor,
        CancellationToken ct)
    {
        var message = new AssetAssignedEvent(
            Guid.NewGuid(),
            asset.Id,
            asset.AssetTag,
            asset.SerialNumber,
            $"{asset.Make} {asset.Model}",
            custodian.Id,
            custodian.Email,
            custodian.Department,
            DateTime.UtcNow,
            actor);

        return publishEndpoint.Publish(message, ct);
    }
}
