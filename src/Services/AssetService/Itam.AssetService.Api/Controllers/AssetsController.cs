using Microsoft.AspNetCore.Mvc;
using Itam.AssetService.Core.Contracts.DTOs;
using Itam.AssetService.Core.Contracts.Interfaces;

namespace Itam.AssetService.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AssetsController(IAssetService assetsService) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetAssets([FromQuery] int page = 1, [FromQuery] int size = 20, [FromQuery] string? status = null)
        {
            var result = await assetsService.SearchAssetAsync(page, size, status);
            return Ok(result);
        }

         [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetAssetById(Guid id)
        {
            var result = await assetsService.GetByIdAsync(id);
            return result.IsSuccess ? Ok(result.Data) : NotFound(new {error = result.ErrorMessage});
        }

         [HttpPost]
        public async Task<IActionResult> CreateAsset([FromBody] CreateAssetRequestDto request)
        {
            var result = await assetsService.CreateAssetAsync(request, User?.Identity?.Name ?? "API_USER");
           
            if(!result.IsSuccess)
                return BadRequest(new {code = result.ErrorCode, error = result.ErrorMessage});

            return CreatedAction(nameof(GetAssetById), new{ id = result.Data!.Id}, result.Data);
        }

         [HttpPost("assign")]
        public async Task<IActionResult> AssignAsset([FromBody] AssignAssetRequestDto request)
        {
            var result = await assetsService.AssignAssetAsync(request, User?.Identity?.Name ?? "API_USER");
           
            if(!result.IsSuccess)
            {
                if(result.ErrorCode == "NOT_FOUND") 
                    return NotFound(new {error = result.ErrorMessage});
                return BadRequest(new { code = result.ErrorCode, error = result.ErrorMessage});
            }
            return Ok(result.Data);
        }

         [HttpPost("{id:guid}/return")]
        public async Task<IActionResult> ReturnAsset(Guid id)
        {
            var result = await assetsService.ReturnAssetAsync(id, User?.Identity?.Name ?? "API_USER");
           
            return result.IsSuccess ? Ok(result.Data) : BadRequest(new {error = result.ErrorMessage});
           
        }
        
    }
}