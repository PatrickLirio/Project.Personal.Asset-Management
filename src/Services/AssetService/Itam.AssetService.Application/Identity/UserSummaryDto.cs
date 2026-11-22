

namespace Itam.AssetService.Application.Identity
{
    public sealed record UserSummaryDto(
    Guid Id, string Email, string? DisplayName, bool IsActive, IReadOnlyList<string> Roles);
 

}