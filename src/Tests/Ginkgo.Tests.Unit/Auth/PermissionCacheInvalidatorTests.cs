using Ginkgo.Api.Auth;
using Microsoft.Extensions.Caching.Memory;

namespace Ginkgo.Tests.Unit.Auth;

public class PermissionCacheInvalidatorTests
{
    [Fact]
    public void InvalidateAll_ShouldMovePermissionCacheToNewGeneration()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var invalidator = new PermissionCacheInvalidator(cache);

        var oldRolesKey = invalidator.UserRolesCacheKey(10001);
        var oldGrantsKey = invalidator.RoleGrantsCacheKey("1,2,3");
        cache.Set(oldRolesKey, new[] { 1L, 2L });
        cache.Set(oldGrantsKey, new HashSet<long> { 10L, 20L });

        invalidator.InvalidateAll();

        Assert.NotEqual(oldRolesKey, invalidator.UserRolesCacheKey(10001));
        Assert.NotEqual(oldGrantsKey, invalidator.RoleGrantsCacheKey("1,2,3"));
    }

    [Fact]
    public void InvalidateUserRoles_ShouldRemoveCurrentGenerationUserRoleCaches()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var invalidator = new PermissionCacheInvalidator(cache);
        var rolesKey = invalidator.UserRolesCacheKey(10001);
        var superAdminKey = invalidator.UserSuperAdminCacheKey(10001);
        cache.Set(rolesKey, new[] { 1L });
        cache.Set(superAdminKey, true);

        invalidator.InvalidateUserRoles(10001);

        Assert.False(cache.TryGetValue(rolesKey, out _));
        Assert.False(cache.TryGetValue(superAdminKey, out _));
    }
}
