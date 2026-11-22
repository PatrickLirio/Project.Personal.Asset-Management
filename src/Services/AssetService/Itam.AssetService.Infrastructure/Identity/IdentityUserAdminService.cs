

using Itam.AssetService.Application.Identity;
using Itam.Common.Base.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Itam.AssetService.Infrastructure.Identity
{
    public sealed class IdentityUserAdminService(UserManager<AssetUser> userManager, ICurrentUser currentUser) : IUserAdminService
    {
    public async Task<PagedResult<UserSummaryDto>> GetUsersAsync(int page, int size, CancellationToken ct)
        {
            if (!currentUser.IsAdmin)
                return new PagedResult<UserSummaryDto>(Array.Empty<UserSummaryDto>(), 0, page, size);
        
            var query = userManager.Users.OrderBy(user => user.Email);
            var total = await query.CountAsync(ct);
            var users = await query.Skip((page - 1) * size).Take(size).ToListAsync(ct);
            var items = new List<UserSummaryDto>();
            foreach (var user in users)
            {
                var roles = await userManager.GetRolesAsync(user);
                items.Add(new UserSummaryDto(user.Id, user.Email ?? "", user.DisplayName,
                    user.IsActive, roles.ToArray()));
            }
            return new PagedResult<UserSummaryDto>(items, total, page, size);
        }
    }

    


}