using Ginkgo.Domain;
using Ginkgo.Domain.Menus;
using Ginkgo.Domain.Utils;
using Serilog;

namespace Ginkgo.Api.Modules;

/// <summary>
/// 模块权限注册器：扫描模块的 IModulePermissionProvider 实现，
/// 自动将声明的权限注册到菜单表。
/// </summary>
public sealed class ModulePermissionRegistrar
{
    private readonly IRepository<Menu> _menuRepo;

    public ModulePermissionRegistrar(IRepository<Menu> menuRepo)
    {
        _menuRepo = menuRepo;
    }

    /// <summary>
    /// 为指定模块注册权限。在模块安装/启用时调用。
    /// </summary>
    public async Task RegisterPermissionsAsync(string moduleId, IReadOnlyList<Ginkgo.Plugin.Abstractions.ModulePermission> permissions)
    {
        foreach (var perm in permissions)
        {
            // 检查是否已存在相同 Resource + Method 的菜单项
            var existing = _menuRepo.Query()
                .Where(m => m.Resource == perm.Resource && m.Method == perm.Method)
                .First();

            if (existing != null)
            {
                Log.Information("[ModulePermissions] Permission already exists: {Resource} {Method}", perm.Resource, perm.Method);
                continue;
            }

            // 查找或创建父级菜单
            long? parentId = null;
            if (!string.IsNullOrWhiteSpace(perm.ParentPath))
            {
                var parent = _menuRepo.Query()
                    .Where(m => m.Name == perm.ParentPath && m.Type == "Directory")
                    .First();
                if (parent != null) parentId = parent.Id;
            }

            var menu = new Menu
            {
                Id = SnowflakeIdGenerator.NextId(),
                Name = perm.DisplayName,
                Type = "Api",
                Resource = perm.Resource,
                Method = perm.Method,
                ParentId = parentId,
                Enabled = true,
                Order = 999
            };

            await _menuRepo.AddAsync(menu);
            Log.Information("[ModulePermissions] Registered: {DisplayName} ({Method} {Resource}) for module {ModuleId}",
                perm.DisplayName, perm.Method, perm.Resource, moduleId);
        }
    }
}
