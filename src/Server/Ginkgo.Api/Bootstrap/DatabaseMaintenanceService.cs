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
        try
        {
            var sugar3 = serviceProvider.GetRequiredService<ISqlSugarClient>();
            sugar3.CodeFirst.InitTables(typeof(Ginkgo.Domain.Auth.RefreshToken));
            Console.WriteLine("[BOOT] RefreshToken table ensured.");
        }
        catch (Exception ex) { Console.WriteLine($"[BOOT] EnsureRefreshTokenTable failed: {ex.Message}"); }

        // 定时任务相关表建表
        try
        {
            var sugar4 = serviceProvider.GetRequiredService<ISqlSugarClient>();
            sugar4.CodeFirst.InitTables(
                typeof(Ginkgo.Domain.Scheduling.ScheduledTaskRecord),
                typeof(Ginkgo.Domain.Scheduling.ScheduledTaskLog));
            Console.WriteLine("[BOOT] ScheduledTask tables ensured.");
        }
        catch (Exception ex) { Console.WriteLine($"[BOOT] EnsureScheduledTaskTables failed: {ex.Message}"); }

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
