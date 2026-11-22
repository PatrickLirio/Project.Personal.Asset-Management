

namespace Itam.AssetService.Application.Identity;
public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc);