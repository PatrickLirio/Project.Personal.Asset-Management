


using Itam.AssetService.Core.Contracts.DTOs;
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
            public string OperationType { get; set; } = "ASSIGN_ASSET"; // Example: CREATE, ASSIGN, RETIRE, etc.

            public AssignAssetRequestDto? AssignmentPayload { get; set; } // Payload for ASSIGN_ASSET operation
            public CreateAssetRequestDto? CreationPayload { get; set; } // Payload for CREATE operation
        }
        public record Response
        {
            public bool Success { get; set; }
            public string StatusCode { get; set; } = "OK"; // Example: "OK", "ERROR", etc.
            public string Message { get; set; } = string.Empty;
            public AssetResponseDto? AssetData { get; set; } // The asset data returned after the operation}
        }
    
    }
}
