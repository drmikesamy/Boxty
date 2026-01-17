
namespace Boxty.ServerBase.Services
{
    public interface IRolePermissionCacheService
    {
        Task InitAsync();
        bool HasPermission(string permissionName, string roleName);
    }
}
