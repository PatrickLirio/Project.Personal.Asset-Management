

namespace Itam.AssetService.Application.Identity
{
    public interface ITokenService
    {
        (string Token, DateTime ExpiresUtc) Create(
        Guid userId, string email, IReadOnlyCollection<string> roles);
    }
}