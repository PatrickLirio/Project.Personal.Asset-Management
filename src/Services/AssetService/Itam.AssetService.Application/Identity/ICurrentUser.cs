

namespace Itam.AssetService.Application.Identity;

    public interface ICurrentUser
    {
        Guid? UserId {get;}
        bool IsAdmin {get;}
    }
