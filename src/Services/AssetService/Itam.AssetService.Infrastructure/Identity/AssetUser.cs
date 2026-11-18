

namespace Itam.AssetService.Infrastructure.Identity
{
    public sealed class AssetUser : IdentityUser<Guid>
{
    public bool IsActive { get; set; } = true;
    public string? DisplayName { get; set; }
}

}