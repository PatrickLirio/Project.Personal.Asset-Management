


using Itam.Common.Messaging.Contracts;

namespace Itam.AssetService.Core.Contracts.Messages
{
    /// <summary>
    /// for reponse body pattern
    /// </summary>
    public class AssetOperationMessage : BaseMessage<AssetOperationMessage.Request, AssetOperationMessage.Response>
    {
        public record Request
        {
            public string? Message { get; init; }
        }
        public record Response
        {
            public string? Message { get; init; }
        }
    
    }
}
