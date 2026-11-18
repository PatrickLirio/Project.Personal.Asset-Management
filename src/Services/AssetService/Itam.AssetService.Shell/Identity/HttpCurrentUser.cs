
using System.Security.Claims;
using Itam.AssetService.Application.Identity;

namespace Itam.AssetService.Shell.Identity
{
    public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
    {
        private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

        public Guid? UserId
        {
            get
            {
                var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                return Guid.TryParse(value, out var id) ? id : null;
            }
        }
        public bool IsAdmin => Principal?.IsInRole("Admin") == true;
    }
}