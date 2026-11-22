

namespace Itam.AssetService.Application.Identity;
public sealed record AdminCreateUserRequest(
    string Email, string DisplayName, string InitialPassword, string Role);
