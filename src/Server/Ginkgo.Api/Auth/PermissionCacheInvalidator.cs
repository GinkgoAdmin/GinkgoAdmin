using Microsoft.Extensions.Caching.Memory;

namespace Ginkgo.Api.Auth;

/// <summary>
/// 权限缓存失效器，用于在角色、权限、菜单、模块安装状态变化时刷新授权数据。
/// </summary>
public sealed class PermissionCacheInvalidator
{
    private readonly IMemoryCache _cache;
    private int _generation;

    public const string MenuCandidatesKey = "perm:menu_candidates";
    public const string UserRolesPrefix = "perm:roles:";
    public const string RoleGrantsPrefix = "perm:grants:";
    public const string UserSuperAdminPrefix = "perm:superadmin:";

    public PermissionCacheInvalidator(IMemoryCache cache)
    {
        _cache = cache;
    }

    public int CurrentGeneration => Volatile.Read(ref _generation);

    public string MenuCandidatesCacheKey => VersionedKey(MenuCandidatesKey);

    public string UserRolesCacheKey(long userId) => VersionedKey($"{UserRolesPrefix}{userId}");

    public string UserSuperAdminCacheKey(long userId) => VersionedKey($"{UserSuperAdminPrefix}{userId}");

    public string RoleGrantsCacheKey(string roleKey) => VersionedKey($"{RoleGrantsPrefix}{roleKey}");

    public void InvalidateMenus()
    {
        _cache.Remove(MenuCandidatesCacheKey);
        _cache.Remove(MenuCandidatesKey);
    }

    public void InvalidateUserRoles(long userId)
    {
        _cache.Remove(UserRolesCacheKey(userId));
        _cache.Remove(UserSuperAdminCacheKey(userId));
        _cache.Remove($"{UserRolesPrefix}{userId}");
        _cache.Remove($"{UserSuperAdminPrefix}{userId}");
    }

    public void InvalidateAll()
    {
        Interlocked.Increment(ref _generation);
        _cache.Remove(MenuCandidatesKey);
    }

    private string VersionedKey(string key) => $"v{CurrentGeneration}:{key}";
}
