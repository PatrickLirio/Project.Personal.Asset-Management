

using Itam.Common.Base.Common;

namespace Itam.AssetService.Application.Identity
{
public interface IAuthenticationService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken ct);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct);
}


}