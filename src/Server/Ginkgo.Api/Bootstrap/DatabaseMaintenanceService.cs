using Ginkgo.Domain;
using Ginkgo.Domain.Menus;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using System;
using System.Linq;

namespace Ginkgo.Api.Bootstrap;

/// <summary>
/// 数据库维护服务：负责应用启动时的自动建表、列修复，以及暴露管理员手动建表/修复的端点。
/// </summary>
public static class DatabaseMaintenanceService
{
    /// <summary>
    /// 执行应用启动时的自动建表和修复逻辑
    /// </summary>
    public static void EnsureDatabaseAndTables(IServiceProvider serviceProvider)
    {
        var cfg = serviceProvider.GetRequiredService<IConfiguration>();
        var autoCreate = string.Equals(cfg["Database:AutoCreate"], "true", StringComparison.OrdinalIgnoreCase);
        var provider = cfg["Database:Provider"] ?? string.Empty;
        var isPostgreSql = provider.Contains("postgre", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("pgsql", StringComparison.OrdinalIgnoreCase);
        
        if (autoCreate)
        {
            var sugar = serviceProvider.GetRequiredService<ISqlSugarClient>();
            try { sugar.DbMaintenance.CreateDatabase(); } catch (Exception ex) { Console.WriteLine($"[BOOT] CreateDatabase failed: {ex.Message}"); }
            try
            {
                // 以 Domain 下继承 Entity 的类型作为建表依据
                var entityBase = typeof(Entity);
                var entityTypes = entityBase.Assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && entityBase.IsAssignableFrom(t))
                    .ToArray();
                sugar.CodeFirst.InitTables(entityTypes);
            }
            catch (Exception ex) { Console.WriteLine($"[BOOT] InitTables failed: {ex.Message}"); }
        }

        // Ensure OpLog.ReviewCN column exists (idempotent)
        try
        {
            var sugar2 = serviceProvider.GetRequiredService<ISqlSugarClient>();
            if (!sugar2.DbMaintenance.IsAnyColumn("ginkgo_Sys_OpLog", "ReviewCN"))
            {
                var col = new SqlSugar.DbColumnInfo
                {
                    DbColumnName = "ReviewCN",
                    DataType = "NVARCHAR",
                    Length = 200,
                    IsNullable = true,
                    ColumnDescription = "中文审记串：模块-功能-结果"
                };
                sugar2.DbMaintenance.AddColumn("ginkgo_Sys_OpLog", col);
            }
        }
        catch (Exception ex) { Console.WriteLine($"[BOOT] EnsureOpLogColumn failed: {ex.Message}"); }

        // Ensure RefreshToken table exists (idempotent, added after initial DB)
        // PostgreSQL 已由全量迁移建表，跳过 CodeFirst 避免小写列名冲突
        if (!isPostgreSql)
        try
        {
            var sugar3 = serviceProvider.GetRequiredService<ISqlSugarClient>();
            sugar3.CodeFirst.InitTables(typeof(Ginkgo.Domain.Auth.RefreshToken));
            Console.WriteLine("[BOOT] RefreshToken table ensured.");
        }
        catch (Exception ex) { Console.WriteLine($"[BOOT] EnsureRefreshTokenTable failed: {ex.Message}"); }

        // 定时任务相关表建表
        if (!isPostgreSql)
        try
        {
            var sugar4 = serviceProvider.GetRequiredService<ISqlSugarClient>();
            sugar4.CodeFirst.InitTables(
                typeof(Ginkgo.Domain.Scheduling.ScheduledTaskRecord),
                typeof(Ginkgo.Domain.Scheduling.ScheduledTaskLog));
            Console.WriteLine("[BOOT] ScheduledTask tables ensured.");
        }
        catch (Exception ex) { Console.WriteLine($"[BOOT] EnsureScheduledTaskTables failed: {ex.Message}"); }

        // 多端通用插件业务入口：菜单组相关表的补列与建表（幂等）。
        // - 为 ginkgo_Sys_MenuGroup 补 IsDefault 列；
        // - 为 ginkgo_Sys_MenuGroupItem 补 Module / RequireGrant / IsUniappHome 列；
        // - 创建 ginkgo_Sys_RoleMenuGroupItem（角色—菜单组项 item 级授权）表。
        // 说明：即使 Database:AutoCreate 关闭，这里也会确保上述列与表存在，便于老库平滑升级。
        if (!isPostgreSql)
        try
        {
            var sugar5 = serviceProvider.GetRequiredService<ISqlSugarClient>();
            sugar5.CodeFirst.InitTables(
                typeof(MenuGroup),
                typeof(MenuGroupItem),
                typeof(RoleMenuGroupItem));
            Console.WriteLine("[BOOT] MenuGroup/MenuGroupItem/RoleMenuGroupItem tables ensured.");
        }
        catch (Exception ex) { Console.WriteLine($"[BOOT] EnsureMenuGroupTables failed: {ex.Message}"); }

        // Ensure MenuGroupItem.IsUniappHome column exists (idempotent)
        try
        {
            var sugar5b = serviceProvider.GetRequiredService<ISqlSugarClient>();
            if (!sugar5b.DbMaintenance.IsAnyColumn("ginkgo_Sys_MenuGroupItem", "IsUniappHome"))
            {
                var col = new DbColumnInfo
                {
                    DbColumnName = "IsUniappHome",
                    DataType = "Boolean",
                    IsNullable = false,
                    DefaultValue = "0",
                    ColumnDescription = "是否设为UNIAPP框架启动首页"
                };
                sugar5b.DbMaintenance.AddColumn("ginkgo_Sys_MenuGroupItem", col);
                Console.WriteLine("[BOOT] MenuGroupItem.IsUniappHome column ensured.");
            }
        }
        catch (Exception ex) { Console.WriteLine($"[BOOT] EnsureMenuGroupItemIsUniappHomeColumn failed: {ex.Message}"); }

        // 补齐模块配置写接口的权限资源，确保老库升级后也能走统一权限链路
        try
        {
            var menuRepo = serviceProvider.GetRequiredService<IRepository<Menu>>();
            SystemMenuPermissionCatalog
                .EnsureModuleManagementApiPermissionsAsync(menuRepo)
                .GetAwaiter()
                .GetResult();
            Console.WriteLine("[BOOT] Module config permission menus ensured.");
        }
        catch (Exception ex) { Console.WriteLine($"[BOOT] EnsureModuleConfigPermissionMenus failed: {ex.Message}"); }

        // 预置三端默认菜单组（移动端 / WEB 前台 / 桌面端），幂等执行，放在建表/补列之后。
        try
        {
            var sugar6 = serviceProvider.GetRequiredService<ISqlSugarClient>();
            EnsureDefaultMenuGroups(sugar6);
        }
        catch (Exception ex) { Console.WriteLine($"[BOOT] EnsureDefaultMenuGroups failed: {ex.Message}"); }
    }

    /// <summary>
    /// 预置三端默认菜单组（幂等）。
    /// <para>
    /// 为移动端（UNIAPP）、WEB 前台（WEB_PORTAL）、桌面端（WPF）各预置一个
    /// <c>IsDefault=1</c>、<c>IsSystem=1</c> 的系统默认菜单组，作为插件业务入口（MenuGroupItem）的注入容器。
    /// </para>
    /// <para>幂等与向后兼容规则：</para>
    /// <list type="number">
    ///   <item>按 <c>Slug</c>（唯一）判断是否已存在，存在则跳过、不重建、不修改既有记录（需求 4.5）。</item>
    ///   <item>若该 Slug 不存在，但同端（同 <c>ClientType</c>）已存在其他 <c>IsDefault=1</c> 的历史菜单组，
    ///         则保留既有默认组并跳过创建，避免为同一终端制造两个默认组（需求 13.5）。</item>
    /// </list>
    /// <para>
    /// 关于模块归属（需求 4.6）：三组在语义上归属主框架数据（<c>'sys'</c>）。
    /// <see cref="MenuGroup"/> 实体本身没有 <c>Module</c> 列（仅其菜单项 <see cref="MenuGroupItem"/> 才有），
    /// 因此该归属语义不落库为列，仅通过“系统内置容器 + 此处文档说明”体现，且不为 <see cref="MenuGroup"/> 新增 Module 列。
    /// </para>
    /// </summary>
    private static void EnsureDefaultMenuGroups(ISqlSugarClient sugar)
    {
        // 三端默认菜单组的预置定义：名称、Slug（唯一）、终端类型。
        var presets = new (string Name, string Slug, string ClientType)[]
        {
            ("默认移动端", "default-uniapp", "UNIAPP"),
            ("默认WEB前台", "default-web-portal", "WEB_PORTAL"),
            ("默认桌面端", "default-wpf", "WPF"),
        };

        foreach (var (name, slug, clientType) in presets)
        {
            // 规则 1：按 Slug 唯一存在性判断（含软删除标记为已存在亦视为存在，避免重复唯一键）。
            var slugExists = sugar.Queryable<MenuGroup>().Any(g => g.Slug == slug);
            if (slugExists)
            {
                Console.WriteLine($"[BOOT] DefaultMenuGroup '{slug}' already exists, skip.");
                continue;
            }

            // 规则 2：若同端已存在其他 IsDefault=1 的历史菜单组，则保留既有并跳过创建（需求 13.5）。
            var hasOtherDefaultForClient = sugar.Queryable<MenuGroup>()
                .Where(g => !g.IsDeleted && g.IsDefault && g.ClientType == clientType)
                .Any();
            if (hasOtherDefaultForClient)
            {
                Console.WriteLine($"[BOOT] ClientType '{clientType}' already has a default MenuGroup, skip creating '{slug}'.");
                continue;
            }

            // 通过领域工厂创建系统内置默认组（IsSystem=1、IsDefault=1）。
            // 注意：MenuGroup.Create 会将 Slug 统一转为小写，预置 Slug 本身即小写，无影响。
            var group = MenuGroup.Create(
                name: name,
                slug: slug,
                clientType: clientType,
                isSystem: true,
                isDefault: true);

            sugar.Insertable(group).ExecuteCommand();
            Console.WriteLine($"[BOOT] DefaultMenuGroup created: {name} ({slug}/{clientType}).");
        }
    }

    /// <summary>
    /// 数据库维护管理员端点。
    /// <para>
    /// 历史上这里曾承载 /admin/create-setting-table 与 /admin/create-notification-tables 两个端点
    /// （内部硬编码 SQL Server 方言裸 DDL），用于早期版本升级时的 schema 漂移修复。
    /// 当前两个端点已被废弃并删除，理由：
    /// </para>
    /// <list type="bullet">
    ///   <item>全仓零引用（前端、UniApp、脚本、文档均无调用）</item>
    ///   <item>对应表结构已在 resource/mysql_install.sql 与 resource/mssql_install.sql 中完整定义，
    ///         配合 EnsureDatabaseAndTables 中的 CodeFirst.InitTables 形成双重覆盖</item>
    ///   <item>原裸 SQL 完全是 SQL Server 方言（uniqueidentifier / sys.objects / sp_rename / rowversion），
    ///         在 MySQL 部署下根本无法执行，长期是死代码</item>
    /// </list>
    /// <para>
    /// 如果未来确实需要应急 schema 修复入口，请通过 IDatabaseDialect.SqlGet... 与
    /// SqlSugar.CodeFirst.InitTables 实现方言无关版本，而非再次内嵌裸 SQL。
    /// </para>
    /// </summary>
    public static void MapAdminEndpoints(WebApplication app)
    {
        // 当前无需注册任何裸 SQL 维护端点。EnsureDatabaseAndTables 已通过 CodeFirst 完成全部建表/补列工作。
    }
}
