

namespace Itam.AssetService.Application.Identity;

public sealed record RegisterUserRequest(string Email, string Password, string DisplayName);
