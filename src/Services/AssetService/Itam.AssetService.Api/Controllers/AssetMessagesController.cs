using Microsoft.AspNetCore.Mvc;
using Itam.AssetService.Core.Contracts.Interfaces;
using Itam.AssetService.Core.Contracts.Messages;

namespace Itam.AssetService.Api.Controllers
{
     [ApiController]
    [Route("api/[controller]")]
    public class AssetMessagesController(IAssetService assetService) : ControllerBase
    {
         [HttpPost("process")]
        public async Task<IActionResult> ProcessMessage([FromBody] AssetOperationMessage message)
        {
            var response = await assetService.ProcessMessageAsync(message.RequestBody , message.CorrelationId);
           
           message.ResponseBody = response;
           return Ok(message);
        }
        
    }
}