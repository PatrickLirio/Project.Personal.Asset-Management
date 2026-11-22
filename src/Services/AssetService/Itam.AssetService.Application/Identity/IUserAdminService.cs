

using Itam.Common.Base.Common;

namespace Itam.AssetService.Application.Identity
{
    public interface IUserAdminService
    {
        Task<PagedResult<UserSummaryDto>> GetUsersAsync(int page, int size, CancellationToken ct);
        Task<Result<Guid>> CreateUserAsync(AdminCreateUserRequest request);
        Task<Result<bool>> DeactivateUserAsync(Guid userId);
        Task<Result<bool>> SetRoleAsync(Guid userId, string role);
    }

}