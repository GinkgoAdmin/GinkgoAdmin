using System.Data.Common;
using System.Text.Json;
using Ginkgo.Domain.Utils;
using Ginkgo.Infrastructure.Abstractions;
using SqlSugar;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Api.Modules;

public sealed class ModuleSqlExecutor
{
    private readonly IServiceProvider _services;
    private readonly IDatabaseDialect _dialect;

    public ModuleSqlExecutor(IServiceProvider services)
    {
        _services = services;
        // 通过方言注册中心按 Database:Provider 解析一次。Dialect 是 Singleton 且无状态，
        // 在整个应用生命周期内复用，避免重复查配置。
        var registry = services.GetRequiredService<IDialectRegistry>();
        var cfg = services.GetRequiredService<IConfiguration>();
        var provider = cfg["Database:Provider"]
            ?? throw new InvalidOperationException("未找到 Database:Provider 配置");
        _dialect = registry.Get(provider);
    }

    /// <summary>
    /// 从 SqlSugar 获取底层 ADO.NET 连接。
    /// </summary>
    private static DbConnection GetSqlSugarConnection(IServiceProvider sp)
    {
        var sugar = sp.GetRequiredService<ISqlSugarClient>();
        return ((SqlSugarClient)sugar).Ado.Connection as DbConnection
            ?? throw new InvalidOperationException("无法从 SqlSugar 获取底层数据库连接");
    }

    /// <summary>
    /// P1-4：危险 SQL 关键字黑名单。模块包安装/升级 SQL 不允许触及实例级管理操作，
    /// 否则单个恶意/失误的模块可以直接删库或越权拿管理员权限。
    /// 命中即立即抛错并回滚整个安装。匹配是大小写无关的"\b关键字\b"形式，
    /// 避免误伤常规标识符（如名为 "drop_log_table" 的列）。
    /// </summary>
    private static readonly string[] _forbiddenSqlKeywords = new[]
    {
        "DROP DATABASE",
        "DROP SCHEMA",
        "CREATE DATABASE",
        "CREATE SCHEMA",
        "ALTER DATABASE",
        "ALTER SCHEMA",
        "SHUTDOWN",
        "GRANT ALL",
        "REVOKE ALL",
        "SET GLOBAL",
        "SET PERSIST",
        "FLUSH PRIVILEGES",
        "LOAD DATA",
        "LOAD_FILE",
        "INTO OUTFILE",
        "INTO DUMPFILE",
        "CREATE USER",
        "DROP USER",
        "ALTER USER",
        "RENAME USER"
    };

    /// <summary>
    /// 校验单个 SQL 批次是否触发了危险关键字黑名单。命中返回触发的关键字，未命中返回 null。
    /// </summary>
    private static string? FindForbiddenKeyword(string batch)
    {
        if (string.IsNullOrWhiteSpace(batch)) return null;
        // 移除注释，避免在 -- DROP DATABASE foo / /* ... */ 里误判
        var stripped = StripSqlComments(batch);
        foreach (var keyword in _forbiddenSqlKeywords)
        {
            // \b 仅识别 ASCII 单词边界，对 SQL 关键字足够
            if (System.Text.RegularExpressions.Regex.IsMatch(
                    stripped,
                    "\\b" + System.Text.RegularExpressions.Regex.Escape(keyword) + "\\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return keyword;
            }
        }
        return null;
    }

    /// <summary>
    /// 简单移除 SQL 注释（-- 行注释、/* ... */ 块注释），仅用于关键字预检，不影响真正执行的 SQL。
    /// </summary>
    private static string StripSqlComments(string sql)
    {
        // 块注释
        var noBlock = System.Text.RegularExpressions.Regex.Replace(sql, "/\\*.*?\\*/", " ",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        // 行注释
        var noLine = System.Text.RegularExpressions.Regex.Replace(noBlock, "--[^\\n]*", " ");
        return noLine;
    }

    /// <summary>
    /// 在事务中按顺序执行所有脚本批次。任意批次失败 → 整体回滚 → 抛出异常。
    /// 同时对每个批次做危险关键字黑名单预检（P1-4），命中即拒绝执行。
    /// </summary>
    public async Task ExecuteScriptsAsync(IEnumerable<string> scriptPaths, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var conn = GetSqlSugarConnection(scope.ServiceProvider);
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        // P1-4：所有模块安装 SQL 在单一事务中执行；任何一步失败立即回滚，
        // 避免出现"前 3 个批次成功落库、第 4 个失败"留下不一致的中间状态。
        using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            foreach (var path in scriptPaths)
            {
                if (!File.Exists(path)) continue;
                var sql = await File.ReadAllTextAsync(path, ct);
                var batchIndex = 0;
                foreach (var raw in _dialect.SplitBatches(sql))
                {
                    batchIndex++;
                    var batch = (raw ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(batch)) continue;
                    var forbidden = FindForbiddenKeyword(batch);
                    if (forbidden != null)
                    {
                        throw new InvalidOperationException(
                            $"模块安装 SQL 触发危险关键字黑名单 '{forbidden}'，已拒绝执行（脚本: {Path.GetFileName(path)}, 批次 {batchIndex}）。");
                    }

                    // MySQL → 目标方言转写 hook（内置方言恒等返回）
                    var executed = _dialect.TranslateMySqlDDL(batch);
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = executed;
                    await cmd.ExecuteNonQueryAsync(ct);
                }
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            try { await tx.RollbackAsync(ct); } catch { /* 回滚失败仅记录到上层异常 */ }
            throw;
        }
    }

    /// <summary>
    /// SQL Dry-Run 预检：在事务中执行所有脚本，完成后回滚，不会实际修改数据库。
    /// 返回 (成功, 错误列表)。
    /// </summary>
    public async Task<(bool Ok, List<string> Errors)> DryRunScriptsAsync(IEnumerable<string> scriptPaths, CancellationToken ct)
    {
        var errors = new List<string>();
        using var scope = _services.CreateScope();
        var conn = GetSqlSugarConnection(scope.ServiceProvider);
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            foreach (var path in scriptPaths)
            {
                if (!File.Exists(path))
                {
                    errors.Add($"脚本文件不存在: {Path.GetFileName(path)}");
                    continue;
                }

                var sql = await File.ReadAllTextAsync(path, ct);
                var batchIndex = 0;
                foreach (var raw in _dialect.SplitBatches(sql))
                {
                    batchIndex++;
                    var batch = (raw ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(batch)) continue;
                    var forbidden = FindForbiddenKeyword(batch);
                    if (forbidden != null)
                    {
                        // P1-4：Dry-Run 也走同一份黑名单，让前端能在执行前看到风险脚本
                        errors.Add($"[{Path.GetFileName(path)} 批次{batchIndex}] 触发危险关键字黑名单 '{forbidden}'，已拒绝执行");
                        continue;
                    }

                    try
                    {
                        // MySQL → 目标方言转写 hook（内置方言恒等返回）
                        var executed = _dialect.TranslateMySqlDDL(batch);
                        using var cmd = conn.CreateCommand();
                        cmd.Transaction = tx;
                        cmd.CommandText = executed;
                        await cmd.ExecuteNonQueryAsync(ct);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"[{Path.GetFileName(path)} 批次{batchIndex}] {ex.Message}");
                    }
                }
            }
        }
        finally
        {
            // 无论成功与否，始终回滚
            try { await tx.RollbackAsync(ct); } catch { }
        }

        return (errors.Count == 0, errors);
    }


    public sealed class InstallSpec
    {
        public string? ModuleId { get; set; }
        public string? Description { get; set; }
        public string? SupportedClients { get; set; }
        public string[]? SqlScripts { get; set; }
        public string[]? UninstallSql { get; set; }
        public Dictionary<string, object>? Config { get; set; }
        public Dictionary<string, string>? Constants { get; set; }
        public MenusSpec? Menus { get; set; }
        // 客户端入口声明段（install.json 的 ClientMenus），独立于既有后台 RBAC 菜单 Menus 段。
        // 用于将插件在各端（WEB_PORTAL/WPF/UNIAPP）的业务入口注入到对应端默认菜单组的 MenuGroupItem。
        public List<ClientMenusSpec>? ClientMenus { get; set; }
    }

    /// <summary>
    /// install.json 中单条客户端入口声明：指定终端类型及其入口项集合。
    /// </summary>
    public sealed class ClientMenusSpec
    {
        // 终端类型：取值范围 WEB_PORTAL / WPF / UNIAPP（区分非法值时跳过并记录警告）。
        public string? ClientType { get; set; }
        // 该端的入口项集合。
        public List<ClientMenuItemSpec>? Items { get; set; }
    }

    /// <summary>
    /// install.json 中单个客户端入口项声明（与设计文档《Components and Interfaces 3.3》一致）。
    /// 该类型仅描述安装清单形状，注入时映射为应用层 <c>Ginkgo.Application.Menus.ClientMenuItemSpec</c>。
    /// </summary>
    public sealed class ClientMenuItemSpec
    {
        // 入口标题（映射写入 MenuGroupItem.Title）。
        public string Title { get; set; } = string.Empty;
        // 入口图标（映射写入 MenuGroupItem.Icon），可选。
        public string? Icon { get; set; }
        // 入口跳转地址（映射写入 MenuGroupItem.Url）。
        public string Path { get; set; } = string.Empty;
        // 是否需要授权（映射写入 MenuGroupItem.RequireGrant）。
        public bool RequireGrant { get; set; }
        // 排序号（映射写入 MenuGroupItem.Order）。
        public int Order { get; set; }
        // 角标文案（映射写入 MenuGroupItem.Badge），可选。
        public string? Badge { get; set; }
        // 父级入口的 Path（父项的 Path）。为空=顶级入口；非空时注入逻辑据此在同组同模块内解析父项并建立层级。
        public string? ParentPath { get; set; }
    }

    public sealed class MenusSpec
    {
        public string? RootCode { get; set; }
        public string? RootName { get; set; }
        public string? RootIcon { get; set; }
        public string? RootSupportedClients { get; set; }
        public List<MenuItemSpec>? Items { get; set; }
    }
    public sealed class MenuItemSpec
    {
        public string Name { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public string Type { get; set; } = "Menu"; // Directory/Menu/Item/Button
        public string? ItemMode { get; set; }
        public string? Icon { get; set; }
        public string? Url { get; set; }
        public string? Code { get; set; }
        public string? Resource { get; set; }
        public string? Method { get; set; }
        public string? ParentCode { get; set; }
        public string? WebRouteUrl { get; set; }
        public string? WebDisplayMode { get; set; }
        public string? SupportedClients { get; set; }
        public bool Hidden { get; set; }
        public int SortOrder { get; set; }
    }

    public static InstallSpec? ReadInstallJson(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            return JsonSerializer.Deserialize<InstallSpec>(json, options);
        }
        catch { return null; }
    }
        public async Task ApplyMenusAsync(InstallSpec? spec, string moduleName, string moduleId, CancellationToken ct)
        {
            if (spec?.Menus == null || string.IsNullOrWhiteSpace(spec.Menus.RootCode)) return;
            // moduleId 用于写入 ginkgo_Sys_Menu.Module，便于插件卸载时按模块清理
            var module = string.IsNullOrWhiteSpace(moduleId) ? "sys" : moduleId;
            using var scope = _services.CreateScope();
            var conn = GetSqlSugarConnection(scope.ServiceProvider);
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);

            // 确定 SupportedClients 值：优先使用 RootSupportedClients，否则使用顶层 SupportedClients
            var rootSupportedClients = !string.IsNullOrWhiteSpace(spec.Menus.RootSupportedClients) 
                ? spec.Menus.RootSupportedClients 
                : spec.SupportedClients;

            // Ensure root menu exists
            long rootId;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "select Id from ginkgo_Sys_Menu where Code=@code";
                var p = cmd.CreateParameter(); p.ParameterName = "@code"; p.Value = spec.Menus.RootCode!; cmd.Parameters.Add(p);
                var r = await cmd.ExecuteScalarAsync(ct);
                if (r != null && long.TryParse(r.ToString(), out rootId))
                {
                    // 根目录已存在，总是更新 SupportedClients、Name、Icon、Module
                    using var uc = conn.CreateCommand();
                    uc.CommandText = $"update ginkgo_Sys_Menu set Module=@Module, SupportedClients=@sc, Name=@Name, Icon=@Icon, UpdatedAt={_dialect.UtcNowExpr} where Id=@Id";
                    var u1 = uc.CreateParameter(); u1.ParameterName = "@Id"; u1.Value = rootId; uc.Parameters.Add(u1);
                    var u2 = uc.CreateParameter(); u2.ParameterName = "@sc"; u2.Value = (object?)rootSupportedClients ?? DBNull.Value; uc.Parameters.Add(u2);
                    var rootName = !string.IsNullOrWhiteSpace(spec.Menus.RootName) ? spec.Menus.RootName : moduleName;
                    var u3 = uc.CreateParameter(); u3.ParameterName = "@Name"; u3.Value = rootName; uc.Parameters.Add(u3);
                    var u4 = uc.CreateParameter(); u4.ParameterName = "@Icon"; u4.Value = (object?)spec.Menus.RootIcon ?? DBNull.Value; uc.Parameters.Add(u4);
                    var u5 = uc.CreateParameter(); u5.ParameterName = "@Module"; u5.Value = module; uc.Parameters.Add(u5);
                    await uc.ExecuteNonQueryAsync(ct);
                }
                else
                {
                    rootId = SnowflakeIdGenerator.NextId();
                    using var ic = conn.CreateCommand();
                    ic.CommandText = $@"insert into ginkgo_Sys_Menu(Id,Module,Name,Route,Type,ItemMode,Icon,Url,ParentId,OrderNo,Visible,Code,CreatedAt,IsDeleted,SupportedClients)
values(@Id,@Module,@Name,@Route,'Directory',NULL,@Icon,NULL,50,1,1,@Code,{_dialect.UtcNowExpr},0,@SupportedClients)";
                    var p1 = ic.CreateParameter(); p1.ParameterName = "@Id"; p1.Value = rootId; ic.Parameters.Add(p1);
                    // 优先使用 RootName，否则使用模块名
                    var rootName = !string.IsNullOrWhiteSpace(spec.Menus.RootName) ? spec.Menus.RootName : moduleName;
                    var p2 = ic.CreateParameter(); p2.ParameterName = "@Name"; p2.Value = rootName; ic.Parameters.Add(p2);
                    var p3 = ic.CreateParameter(); p3.ParameterName = "@Route"; p3.Value = spec.Menus.RootCode!; ic.Parameters.Add(p3);
                    var p4 = ic.CreateParameter(); p4.ParameterName = "@Code"; p4.Value = spec.Menus.RootCode!; ic.Parameters.Add(p4);
                    var p5 = ic.CreateParameter(); p5.ParameterName = "@Icon"; p5.Value = (object?)spec.Menus.RootIcon ?? DBNull.Value; ic.Parameters.Add(p5);
                    var p6 = ic.CreateParameter(); p6.ParameterName = "@SupportedClients"; p6.Value = (object?)rootSupportedClients ?? DBNull.Value; ic.Parameters.Add(p6);
                    var p7 = ic.CreateParameter(); p7.ParameterName = "@Module"; p7.Value = module; ic.Parameters.Add(p7);
                    await ic.ExecuteNonQueryAsync(ct);
                }
            }
            if (spec.Menus.Items == null || spec.Menus.Items.Count == 0) return;
            foreach (var it in spec.Menus.Items)
            {
                // Resolve parent by ParentCode
                long parentId = rootId;
                if (!string.IsNullOrWhiteSpace(it.ParentCode))
                {
                    using var pc = conn.CreateCommand();
                    pc.CommandText = "select Id from ginkgo_Sys_Menu where Code=@code";
                    var pp = pc.CreateParameter(); pp.ParameterName = "@code"; pp.Value = it.ParentCode!; pc.Parameters.Add(pp);
                    var pr = await pc.ExecuteScalarAsync(ct);
                    if (pr != null && long.TryParse(pr.ToString(), out var pid)) parentId = pid;
                }
                // 检查是否已存在（按 Route 匹配），已存在则更新关键字段
                if (!string.IsNullOrWhiteSpace(it.Route))
                {
                    using var ec = conn.CreateCommand();
                    ec.CommandText = "select Id, IsDeleted from ginkgo_Sys_Menu where Route=@route";
                    var pr = ec.CreateParameter(); pr.ParameterName = "@route"; pr.Value = it.Route; ec.Parameters.Add(pr);
                    using var reader = await ec.ExecuteReaderAsync(ct);
                    if (await reader.ReadAsync(ct))
                    {
                        var existingId = reader.GetInt64(0);
                        var isDeleted = reader.GetBoolean(1);
                        await reader.CloseAsync();
                        
                        // 无论是否软删除，都更新关键字段（SupportedClients、Resource、Method 等）
                        using var uc = conn.CreateCommand();
                        uc.CommandText = $@"update ginkgo_Sys_Menu set 
                            IsDeleted=0, Visible=@Visible, Name=@Name, OrderNo=@OrderNo,
                            WebRouteUrl=@WebRouteUrl, WebDisplayMode=@WebDisplayMode, 
                            SupportedClients=@SupportedClients, Resource=@Resource, Method=@Method,
                            ParentId=@ParentId, Module=@Module, UpdatedAt={_dialect.UtcNowExpr} where Id=@Id";
                        var u1 = uc.CreateParameter(); u1.ParameterName = "@Id"; u1.Value = existingId; uc.Parameters.Add(u1);
                        var u2 = uc.CreateParameter(); u2.ParameterName = "@Visible"; u2.Value = it.Hidden ? 0 : 1; uc.Parameters.Add(u2);
                        var u3 = uc.CreateParameter(); u3.ParameterName = "@Name"; u3.Value = it.Name; uc.Parameters.Add(u3);
                        var u4 = uc.CreateParameter(); u4.ParameterName = "@OrderNo"; u4.Value = it.SortOrder; uc.Parameters.Add(u4);
                        var u5 = uc.CreateParameter(); u5.ParameterName = "@WebRouteUrl"; u5.Value = (object?)it.WebRouteUrl ?? DBNull.Value; uc.Parameters.Add(u5);
                        var u6 = uc.CreateParameter(); u6.ParameterName = "@WebDisplayMode"; u6.Value = (object?)it.WebDisplayMode ?? DBNull.Value; uc.Parameters.Add(u6);
                        var u7 = uc.CreateParameter(); u7.ParameterName = "@SupportedClients"; u7.Value = (object?)it.SupportedClients ?? DBNull.Value; uc.Parameters.Add(u7);
                        var u8 = uc.CreateParameter(); u8.ParameterName = "@Resource"; u8.Value = (object?)it.Resource ?? DBNull.Value; uc.Parameters.Add(u8);
                        var u9 = uc.CreateParameter(); u9.ParameterName = "@Method"; u9.Value = (object?)it.Method ?? DBNull.Value; uc.Parameters.Add(u9);
                        var u10 = uc.CreateParameter(); u10.ParameterName = "@ParentId"; u10.Value = parentId; uc.Parameters.Add(u10);
                        var u11 = uc.CreateParameter(); u11.ParameterName = "@Module"; u11.Value = module; uc.Parameters.Add(u11);
                        await uc.ExecuteNonQueryAsync(ct);
                        continue;
                    }
                    await reader.CloseAsync();
                }
                // Insert（新增菜单，包含 Resource 和 Method）
                using var ic2 = conn.CreateCommand();
                ic2.CommandText = $@"insert into ginkgo_Sys_Menu(Id,Module,Name,Route,Type,ItemMode,Icon,Url,ParentId,OrderNo,Visible,Code,CreatedAt,IsDeleted,WebRouteUrl,WebDisplayMode,SupportedClients,Resource,Method)
values(@Id,@Module,@Name,@Route,@Type,@ItemMode,@Icon,@Url,@ParentId,@OrderNo,@Visible,@Code,{_dialect.UtcNowExpr},0,@WebRouteUrl,@WebDisplayMode,@SupportedClients,@Resource,@Method)";
                var q1 = ic2.CreateParameter(); q1.ParameterName = "@Id"; q1.Value = SnowflakeIdGenerator.NextId(); ic2.Parameters.Add(q1);
                var q2 = ic2.CreateParameter(); q2.ParameterName = "@Name"; q2.Value = it.Name; ic2.Parameters.Add(q2);
                var q3 = ic2.CreateParameter(); q3.ParameterName = "@Route"; q3.Value = it.Route ?? string.Empty; ic2.Parameters.Add(q3);
                var q4 = ic2.CreateParameter(); q4.ParameterName = "@Type"; q4.Value = it.Type ?? "Menu"; ic2.Parameters.Add(q4);
                var q5 = ic2.CreateParameter(); q5.ParameterName = "@ItemMode"; q5.Value = (object?)it.ItemMode ?? DBNull.Value; ic2.Parameters.Add(q5);
                var q6 = ic2.CreateParameter(); q6.ParameterName = "@Icon"; q6.Value = (object?)it.Icon ?? DBNull.Value; ic2.Parameters.Add(q6);
                var q7 = ic2.CreateParameter(); q7.ParameterName = "@Url"; q7.Value = (object?)it.Url ?? DBNull.Value; ic2.Parameters.Add(q7);
                var q8 = ic2.CreateParameter(); q8.ParameterName = "@ParentId"; q8.Value = parentId; ic2.Parameters.Add(q8);
                var q9 = ic2.CreateParameter(); q9.ParameterName = "@Code"; q9.Value = string.IsNullOrWhiteSpace(it.Code) ? (string.IsNullOrWhiteSpace(it.Route) ? (object)DBNull.Value : it.Route) : it.Code; ic2.Parameters.Add(q9);
                var q10 = ic2.CreateParameter(); q10.ParameterName = "@WebRouteUrl"; q10.Value = (object?)it.WebRouteUrl ?? DBNull.Value; ic2.Parameters.Add(q10);
                var q11 = ic2.CreateParameter(); q11.ParameterName = "@WebDisplayMode"; q11.Value = (object?)it.WebDisplayMode ?? DBNull.Value; ic2.Parameters.Add(q11);
                var q12 = ic2.CreateParameter(); q12.ParameterName = "@SupportedClients"; q12.Value = (object?)it.SupportedClients ?? DBNull.Value; ic2.Parameters.Add(q12);
                var q13 = ic2.CreateParameter(); q13.ParameterName = "@OrderNo"; q13.Value = it.SortOrder; ic2.Parameters.Add(q13);
                // Visible=1 表示启用，Hidden=true 时设为 0
                var q14 = ic2.CreateParameter(); q14.ParameterName = "@Visible"; q14.Value = it.Hidden ? 0 : 1; ic2.Parameters.Add(q14);
                var q15 = ic2.CreateParameter(); q15.ParameterName = "@Resource"; q15.Value = (object?)it.Resource ?? DBNull.Value; ic2.Parameters.Add(q15);
                var q16 = ic2.CreateParameter(); q16.ParameterName = "@Method"; q16.Value = (object?)it.Method ?? DBNull.Value; ic2.Parameters.Add(q16);
                var q17 = ic2.CreateParameter(); q17.ParameterName = "@Module"; q17.Value = module; ic2.Parameters.Add(q17);
                await ic2.ExecuteNonQueryAsync(ct);
            }
        }

        public async Task RemoveMenusAsync(InstallSpec? spec, CancellationToken ct)
        {
            if (spec?.Menus == null) return;
            
            using var scope = _services.CreateScope();
            var conn = GetSqlSugarConnection(scope.ServiceProvider);
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);

            // 方法1：如果有 RootCode，递归删除整个菜单树
            if (!string.IsNullOrWhiteSpace(spec.Menus.RootCode))
            {
                // 使用跨库兼容的迭代查询获取所有子节点ID
                var allIds = await CollectMenuTreeIdsByCodeAsync(conn, spec.Menus.RootCode, ct);
                if (allIds.Count > 0)
                {
                    var idstr = string.Join(",", allIds);
                    // 1. 删除级联的角色权限记录以免触发外键错误
                    using (var dp = conn.CreateCommand())
                    {
                        dp.CommandText = $"DELETE FROM ginkgo_Sys_RolePermission WHERE PermissionId IN ({idstr})";
                        await dp.ExecuteNonQueryAsync(ct);
                    }
                    // 2. 删除菜单记录
                    using (var dm = conn.CreateCommand())
                    {
                        dm.CommandText = $"DELETE FROM ginkgo_Sys_Menu WHERE Id IN ({idstr})";
                        await dm.ExecuteNonQueryAsync(ct);
                    }
                }
                return;
            }

            // 方法2：按 Route 逐个删除（兼容旧逻辑）
            if (spec.Menus.Items == null || spec.Menus.Items.Count == 0) return;
            foreach (var it in spec.Menus.Items)
            {
                if (string.IsNullOrWhiteSpace(it.Route)) continue;
                
                long menuId = 0;
                using (var qc = conn.CreateCommand())
                {
                    qc.CommandText = "SELECT Id FROM ginkgo_Sys_Menu WHERE Route = @route";
                    var pCode = qc.CreateParameter(); pCode.ParameterName = "@route"; pCode.Value = it.Route; qc.Parameters.Add(pCode);
                    var res = await qc.ExecuteScalarAsync(ct);
                    if (res != null) long.TryParse(res.ToString(), out menuId);
                }

                if (menuId > 0)
                {
                    // 1. 删除该菜单相关的角色权限配置
                    using (var dp = conn.CreateCommand())
                    {
                        dp.CommandText = "DELETE FROM ginkgo_Sys_RolePermission WHERE PermissionId = @id";
                        var pid = dp.CreateParameter(); pid.ParameterName = "@id"; pid.Value = menuId; dp.Parameters.Add(pid);
                        await dp.ExecuteNonQueryAsync(ct);
                    }
                    // 2. 删除菜单本身
                    using (var dc = conn.CreateCommand())
                    {
                        dc.CommandText = "DELETE FROM ginkgo_Sys_Menu WHERE Id = @id";
                        var pid = dc.CreateParameter(); pid.ParameterName = "@id"; pid.Value = menuId; dc.Parameters.Add(pid);
                        await dc.ExecuteNonQueryAsync(ct);
                    }
                }
            }
        }
        /// <summary>
        /// 仅根据 RootCode 递归移除整棵菜单树（含角色权限），不依赖 InstallSpec。
        /// 用于卸载时的可靠兜底：即使 install.json 不存在，只要 MenuRootCode 已持久化即可移除。
        /// </summary>
        public async Task RemoveMenusByRootCodeAsync(string rootCode, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(rootCode)) return;

            using var scope = _services.CreateScope();
            var conn = GetSqlSugarConnection(scope.ServiceProvider);
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);

            var allIds = await CollectMenuTreeIdsByCodeAsync(conn, rootCode, ct);
            if (allIds.Count > 0)
            {
                var idstr = string.Join(",", allIds);
                // 1. 删除级联的角色权限记录
                using (var dp = conn.CreateCommand())
                {
                    dp.CommandText = $"DELETE FROM ginkgo_Sys_RolePermission WHERE PermissionId IN ({idstr})";
                    await dp.ExecuteNonQueryAsync(ct);
                }
                // 2. 删除菜单记录
                using (var dm = conn.CreateCommand())
                {
                    dm.CommandText = $"DELETE FROM ginkgo_Sys_Menu WHERE Id IN ({idstr})";
                    await dm.ExecuteNonQueryAsync(ct);
                }
            }
        }

        // ============================================================
        // 客户端入口（MenuGroupItem）安装/清理：独立于 ginkgo_Sys_Menu 的后台 RBAC 菜单逻辑，
        // 复用应用层 IMenuGroupAppService 走既有租户隔离链路写入默认菜单组。
        // ============================================================

        /// <summary>
        /// 解析 install.json 的 ClientMenus 段，并将各端入口项注入到对应端的默认菜单组（MenuGroupItem）。
        /// 行为约定（对应需求 5.4/5.5/5.6/5.7、6.1/6.2/6.3/6.4/6.5/6.6）：
        ///   - ClientMenus 为空 → 直接返回，不影响既有 Menus 段处理（需求 5.7）。
        ///   - 逐条校验 ClientType ∈ {WEB_PORTAL, WPF, UNIAPP}（大小写不敏感）：非法则记录警告日志并跳过该条（需求 5.4）；
        ///     警告日志写入若抛出异常，则任其向上传播以中止本次安装（需求 5.6，故不吞掉日志调用异常）。
        ///   - 合法但该端无 IsDefault=1 默认组 → 记录警告日志、不创建任何入口项、跳过（需求 6.5）。
        ///   - 合法且有默认组 → 映射为应用层规格并调用 UpsertClientMenuItemsAsync 注入（需求 6.1/6.3/6.4/6.6），
        ///     成功后记录含 clientType / Module / 注入数量的处理日志（需求 5.5）。
        /// 注意：moduleId 原样传递（区分大小写），与 module.json 的 Id 完全一致。
        /// </summary>
        public async Task ApplyClientMenusAsync(InstallSpec? spec, string moduleId, CancellationToken ct)
        {
            // 需求 5.7：未声明 ClientMenus 段则不注入任何客户端入口项，且不影响既有 Menus 段处理
            if (spec?.ClientMenus == null || spec.ClientMenus.Count == 0) return;

            // 合法终端类型集合（大小写不敏感比较）
            var allowedClientTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "WEB_PORTAL", "WPF", "UNIAPP"
            };

            // IMenuGroupAppService 为 Scoped，ModuleSqlExecutor 为 Singleton，
            // 因此在方法内创建 scope 解析，避免捕获式依赖（与本类既有 scope 使用风格一致）。
            using var scope = _services.CreateScope();
            var sp = scope.ServiceProvider;
            var menuGroupService = sp.GetRequiredService<Ginkgo.Application.Menus.IMenuGroupAppService>();
            var logger = sp.GetRequiredService<ILogger<ModuleSqlExecutor>>();

            foreach (var decl in spec.ClientMenus)
            {
                var clientType = decl?.ClientType?.Trim();

                // 需求 5.4：非法 clientType → 记录警告日志并跳过；
                // 需求 5.6：不对警告日志调用做 try/catch 吞没，写入失败时异常向上传播中止安装。
                if (string.IsNullOrWhiteSpace(clientType) || !allowedClientTypes.Contains(clientType))
                {
                    logger.LogWarning(
                        "插件安装：ClientMenus 声明的 clientType 非法（Module={Module}, clientType={ClientType}），已跳过该声明。",
                        moduleId, clientType ?? "(null)");
                    continue;
                }

                // 归一化为大写规范值，传入应用层服务（其内部按归一化值匹配默认组）
                var normalizedClientType = clientType.ToUpperInvariant();

                // 需求 6.5：该端无 IsDefault=1 默认组 → 记录警告日志、不创建任何项、跳过
                var defaultGroupId = await menuGroupService.GetDefaultGroupIdAsync(normalizedClientType, ct);
                if (defaultGroupId == null)
                {
                    logger.LogWarning(
                        "插件安装：终端类型 {ClientType} 不存在默认菜单组（Module={Module}），已跳过该端入口注入、未创建任何入口项。",
                        normalizedClientType, moduleId);
                    continue;
                }

                // 映射安装清单规格 → 应用层规格（同名类型分属不同命名空间，显式以全限定名映射）
                var items = decl!.Items ?? new List<ClientMenuItemSpec>();
                var appSpecs = items.Select(it => new Ginkgo.Application.Menus.ClientMenuItemSpec
                {
                    Title = it.Title,
                    Icon = it.Icon,
                    Path = it.Path,
                    RequireGrant = it.RequireGrant,
                    Order = it.Order,
                    Badge = it.Badge,
                    ParentPath = it.ParentPath
                }).ToList();

                // 需求 6.1/6.3/6.4/6.6：注入到该端默认组并 upsert，走既有租户隔离链路
                await menuGroupService.UpsertClientMenuItemsAsync(normalizedClientType, moduleId, appSpecs, ct);

                // 需求 5.5：成功注入后记录含 clientType / Module / 数量的处理日志
                logger.LogInformation(
                    "插件安装：已注入客户端入口项（clientType={ClientType}, Module={Module}, 数量={Count}）。",
                    normalizedClientType, moduleId, appSpecs.Count);
            }
        }

        /// <summary>
        /// 按 Module 清理插件注入的全部客户端入口项（MenuGroupItem）及其角色授权关联（RoleMenuGroupItem）。
        /// 委托应用层 RemoveClientMenuItemsByModuleAsync 执行：删除 Module=moduleId 的入口项及其授权关联，
        /// 不触碰 Module='sys' 项、不删除任何 MenuGroup 记录（需求 7.1/7.2/7.3/7.4）。
        /// 可并入既有 RemoveModuleDataAsync 调用序列。moduleId 原样传递（区分大小写）。
        /// </summary>
        public async Task RemoveClientMenusByModuleAsync(string moduleId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(moduleId)) return;

            using var scope = _services.CreateScope();
            var menuGroupService = scope.ServiceProvider
                .GetRequiredService<Ginkgo.Application.Menus.IMenuGroupAppService>();
            await menuGroupService.RemoveClientMenuItemsByModuleAsync(moduleId, ct);
        }

        /// <summary>
        /// 按 Module 字段统一清理插件在主框架共享表中的数据。
        /// 涉及表：
        ///   - ginkgo_Sys_Menu            （顺带清理 ginkgo_Sys_RolePermission 中对应的菜单授权）
        ///   - ginkgo_Sys_DictionaryItem  （先清子表再清父表，避免外键约束）
        ///   - ginkgo_Sys_Dictionary
        ///   - ginkgo_Sys_Settings
        /// 设计目的：让插件无需重建菜单/字典/配置表，可以直接复用主框架表，
        /// 卸载时再按 Module = ModuleId 一次性清理，做到“安装登记、卸载归零”。
        /// </summary>
        public async Task RemoveModuleDataAsync(string moduleId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(moduleId)) return;
            // 保留主框架内置数据：禁止误删 sys 级别记录
            if (string.Equals(moduleId, "sys", StringComparison.OrdinalIgnoreCase)) return;

            using var scope = _services.CreateScope();
            var conn = GetSqlSugarConnection(scope.ServiceProvider);
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);

            // 1. 收集属于该模块的菜单 Id（用于一并清理 ginkgo_Sys_RolePermission）
            var menuIds = new List<long>();
            using (var qc = conn.CreateCommand())
            {
                qc.CommandText = "SELECT Id FROM ginkgo_Sys_Menu WHERE Module=@m";
                var p = qc.CreateParameter(); p.ParameterName = "@m"; p.Value = moduleId; qc.Parameters.Add(p);
                using var reader = await qc.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    menuIds.Add(reader.GetInt64(0));
                }
            }
            if (menuIds.Count > 0)
            {
                var idstr = string.Join(",", menuIds);
                using (var dp = conn.CreateCommand())
                {
                    dp.CommandText = $"DELETE FROM ginkgo_Sys_RolePermission WHERE PermissionId IN ({idstr})";
                    await dp.ExecuteNonQueryAsync(ct);
                }
                using (var dm = conn.CreateCommand())
                {
                    dm.CommandText = $"DELETE FROM ginkgo_Sys_Menu WHERE Id IN ({idstr})";
                    await dm.ExecuteNonQueryAsync(ct);
                }
            }

            // 2. 字典条目：先按 Module 清理，再清理仍挂在该模块字典下的孤儿条目
            using (var di = conn.CreateCommand())
            {
                di.CommandText = "DELETE FROM ginkgo_Sys_DictionaryItem WHERE Module=@m OR DictionaryId IN (SELECT Id FROM ginkgo_Sys_Dictionary WHERE Module=@m)";
                var p = di.CreateParameter(); p.ParameterName = "@m"; p.Value = moduleId; di.Parameters.Add(p);
                try { await di.ExecuteNonQueryAsync(ct); }
                catch
                {
                    // 某些 MySQL 版本不允许 DELETE 子查询自指目标表，回退两步
                    using var step1 = conn.CreateCommand();
                    step1.CommandText = "DELETE FROM ginkgo_Sys_DictionaryItem WHERE Module=@m";
                    var sp1 = step1.CreateParameter(); sp1.ParameterName = "@m"; sp1.Value = moduleId; step1.Parameters.Add(sp1);
                    await step1.ExecuteNonQueryAsync(ct);

                    using var step2 = conn.CreateCommand();
                    step2.CommandText = "DELETE FROM ginkgo_Sys_DictionaryItem WHERE DictionaryId IN (SELECT Id FROM ginkgo_Sys_Dictionary WHERE Module=@m)";
                    var sp2 = step2.CreateParameter(); sp2.ParameterName = "@m"; sp2.Value = moduleId; step2.Parameters.Add(sp2);
                    await step2.ExecuteNonQueryAsync(ct);
                }
            }

            // 3. 字典分类
            using (var dd = conn.CreateCommand())
            {
                dd.CommandText = "DELETE FROM ginkgo_Sys_Dictionary WHERE Module=@m";
                var p = dd.CreateParameter(); p.ParameterName = "@m"; p.Value = moduleId; dd.Parameters.Add(p);
                await dd.ExecuteNonQueryAsync(ct);
            }

            // 4. 系统配置
            using (var ds = conn.CreateCommand())
            {
                ds.CommandText = "DELETE FROM ginkgo_Sys_Settings WHERE Module=@m";
                var p = ds.CreateParameter(); p.ParameterName = "@m"; p.Value = moduleId; ds.Parameters.Add(p);
                await ds.ExecuteNonQueryAsync(ct);
            }
        }

        public async Task SetMenuTreeVisibleAsync(string rootCode, bool visible, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(rootCode)) return;
            using var scope = _services.CreateScope();
            var conn = GetSqlSugarConnection(scope.ServiceProvider);
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);

            // resolve rootId by RootCode
            long rootId;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "select Id from ginkgo_Sys_Menu where Code=@code";
                var p = cmd.CreateParameter(); p.ParameterName = "@code"; p.Value = rootCode; cmd.Parameters.Add(p);
                var r = await cmd.ExecuteScalarAsync(ct);
                if (r == null || !long.TryParse(r.ToString(), out rootId)) return;
            }

            // recursive update visible
            // 方言能力判定：支持递归 CTE 走 CTE；否则走 BFS 迭代藏底（兼容 MySQL 5.7）。
            if (_dialect.Capabilities.SupportsRecursiveCte)
            {
                using var uc = conn.CreateCommand();
                uc.CommandText = @"with cte as (
    select Id from ginkgo_Sys_Menu where Id=@root
    union all
    select m.Id from ginkgo_Sys_Menu m inner join cte on m.ParentId = cte.Id
)
update ginkgo_Sys_Menu set Visible=@vis where Id in (select Id from cte)";
                var p1 = uc.CreateParameter(); p1.ParameterName = "@root"; p1.Value = rootId; uc.Parameters.Add(p1);
                var p2 = uc.CreateParameter(); p2.ParameterName = "@vis"; p2.Value = visible ? 1 : 0; uc.Parameters.Add(p2);
                await uc.ExecuteNonQueryAsync(ct);
            }
            else
            {
                var allIds = await CollectMenuTreeIdsByIdAsync(conn, rootId, ct);
                if (allIds.Count > 0)
                {
                    using var uc = conn.CreateCommand();
                    uc.CommandText = $"UPDATE ginkgo_Sys_Menu SET Visible=@vis WHERE Id IN ({string.Join(",", allIds)})";
                    var p2 = uc.CreateParameter(); p2.ParameterName = "@vis"; p2.Value = visible ? 1 : 0; uc.Parameters.Add(p2);
                    await uc.ExecuteNonQueryAsync(ct);
                }
            }
        }


        /// <summary>
        /// MySQL 兼容：通过 Code 查找根节点，然后迭代收集整棵菜单树的所有 Id
        /// </summary>
        private async Task<List<long>> CollectMenuTreeIdsByCodeAsync(System.Data.Common.DbConnection conn, string rootCode, CancellationToken ct)
        {
            long rootId;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT Id FROM ginkgo_Sys_Menu WHERE Code = @code";
                var p = cmd.CreateParameter(); p.ParameterName = "@code"; p.Value = rootCode; cmd.Parameters.Add(p);
                var r = await cmd.ExecuteScalarAsync(ct);
                if (r == null || !long.TryParse(r.ToString(), out rootId)) return new List<long>();
            }
            return await CollectMenuTreeIdsByIdAsync(conn, rootId, ct);
        }

        /// <summary>
        /// MySQL 兼容：从指定 Id 开始，BFS 迭代收集整棵菜单子树的所有 Id
        /// </summary>
        private async Task<List<long>> CollectMenuTreeIdsByIdAsync(System.Data.Common.DbConnection conn, long rootId, CancellationToken ct)
        {
            var allIds = new List<long> { rootId };
            var currentLevel = new List<long> { rootId };
            while (currentLevel.Count > 0)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT Id FROM ginkgo_Sys_Menu WHERE ParentId IN ({string.Join(",", currentLevel)})";
                var nextLevel = new List<long>();
                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    nextLevel.Add(reader.GetInt64(0));
                }
                allIds.AddRange(nextLevel);
                currentLevel = nextLevel;
            }
            return allIds;
        }

        // ============================================================
        // 数据库导出相关方法（供打包服务使用）
        // ============================================================

        /// <summary>
        /// 按表名前缀从数据库中查找所有匹配的表
        /// </summary>
        public async Task<List<string>> FindTablesByPrefixAsync(string prefix, CancellationToken ct)
        {
            using var scope = _services.CreateScope();
            var conn = GetSqlSugarConnection(scope.ServiceProvider);
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);

            var tables = new List<string>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = _dialect.SqlListTablesByPrefix;
            var p = cmd.CreateParameter();
            p.ParameterName = "@prefix";
            p.Value = prefix.TrimEnd('%') + "%";
            cmd.Parameters.Add(p);

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                tables.Add(reader.GetString(0));
            }
            return tables;
        }

        /// <summary>
        /// 从 SQL 文件内容中提取表名（支持 CREATE TABLE IF NOT EXISTS `xxx` 和 CREATE TABLE `xxx` 语法）
        /// </summary>
        public static List<string> ExtractTableNamesFromSql(string sqlContent)
        {
            var tableNames = new List<string>();
            var regex = new System.Text.RegularExpressions.Regex(
                @"CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?[`\[\""']?(\w+)[`\]\""']?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
            foreach (System.Text.RegularExpressions.Match match in regex.Matches(sqlContent))
            {
                if (match.Groups.Count > 1)
                {
                    tableNames.Add(match.Groups[1].Value);
                }
            }
            return tableNames;
        }

        /// <summary>
        /// 导出指定表的建表语句（表结构）
        /// </summary>
        public async Task<string> ExportTableSchemaAsync(List<string> tableNames, CancellationToken ct)
        {
            if (tableNames == null || tableNames.Count == 0) return string.Empty;

            using var scope = _services.CreateScope();
            var conn = GetSqlSugarConnection(scope.ServiceProvider);
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("-- ============================================================");
            sb.AppendLine("-- 模块表结构导出（自动生成，请勿手动修改）");
            sb.AppendLine($"-- 导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("-- ============================================================");
            sb.AppendLine();

            foreach (var tableName in tableNames)
            {
                try
                {
                    // MySQL 下使用 SHOW CREATE TABLE；其他方言走 INFORMATION_SCHEMA 构建 MySQL 兼容 DDL。
                    // 这里按 _dialect.Code 分流；后续可抽象为 dialect.GetCreateTableSqlAsync 以进一步净化。
                    if (string.Equals(_dialect.Code, "mysql", StringComparison.OrdinalIgnoreCase))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = $"SHOW CREATE TABLE {_dialect.QuoteIdentifier(tableName)}";
                        using var reader = await cmd.ExecuteReaderAsync(ct);
                        if (await reader.ReadAsync(ct))
                        {
                            var createSql = reader.GetString(1);
                            // 将 CREATE TABLE 替换为 CREATE TABLE IF NOT EXISTS
                            createSql = System.Text.RegularExpressions.Regex.Replace(
                                createSql, @"^CREATE TABLE", "CREATE TABLE IF NOT EXISTS",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            sb.AppendLine(createSql + ";");
                            sb.AppendLine();
                        }
                    }
                    else
                    {
                        // 非 MySQL（当前主要是 SQL Server）：从 INFORMATION_SCHEMA 构建 MySQL 兼容 DDL
                        sb.AppendLine($"-- 表: {tableName}");
                        await ExportSqlServerTableSchemaAsync(conn, tableName, sb, ct);
                        sb.AppendLine();
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"-- 导出表 {tableName} 结构失败: {ex.Message}");
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// SQL Server 下通过 INFORMATION_SCHEMA 构建 MySQL 兼容的 CREATE TABLE 语句
        /// </summary>
        private async Task ExportSqlServerTableSchemaAsync(DbConnection conn, string tableName, System.Text.StringBuilder sb, CancellationToken ct)
        {
            // 获取列信息
            var columns = new List<(string Name, string DataType, int? MaxLen, int? NumericPrec, int? NumericScale, string IsNullable, string? Default, string? Extra)>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, 
                    NUMERIC_PRECISION, NUMERIC_SCALE, IS_NULLABLE, COLUMN_DEFAULT
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = @tableName ORDER BY ORDINAL_POSITION";
                var p = cmd.CreateParameter(); p.ParameterName = "@tableName"; p.Value = tableName; cmd.Parameters.Add(p);
                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    columns.Add((
                        Name: reader.GetString(0),
                        DataType: reader.GetString(1),
                        MaxLen: reader.IsDBNull(2) ? null : (int?)Convert.ToInt32(reader.GetValue(2)),
                        NumericPrec: reader.IsDBNull(3) ? null : (int?)Convert.ToInt32(reader.GetValue(3)),
                        NumericScale: reader.IsDBNull(4) ? null : (int?)Convert.ToInt32(reader.GetValue(4)),
                        IsNullable: reader.GetString(5),
                        Default: reader.IsDBNull(6) ? null : reader.GetValue(6)?.ToString(),
                        Extra: null
                    ));
                }
            }

            if (columns.Count == 0)
            {
                sb.AppendLine($"-- 表 {tableName} 不存在或无列");
                return;
            }

            // 获取主键列
            var pkColumns = new List<string>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
                    WHERE TABLE_NAME = @tableName AND CONSTRAINT_NAME LIKE 'PK%' 
                    ORDER BY ORDINAL_POSITION";
                var p = cmd.CreateParameter(); p.ParameterName = "@tableName"; p.Value = tableName; cmd.Parameters.Add(p);
                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    pkColumns.Add(reader.GetString(0));
            }

            // 映射 SQL Server 类型到 MySQL 类型
            string MapType(string name, string dataType, int? maxLen, int? numPrec, int? numScale)
            {
                return dataType.ToLowerInvariant() switch
                {
                    "bigint" => "BIGINT",
                    "int" => "INT",
                    "smallint" => "SMALLINT",
                    "tinyint" => "TINYINT(1)",
                    "bit" => "TINYINT(1)",
                    "decimal" or "numeric" => $"DECIMAL({numPrec ?? 18},{numScale ?? 2})",
                    "nvarchar" or "varchar" => maxLen == -1 ? "TEXT" : $"VARCHAR({maxLen ?? 255})",
                    "nchar" or "char" => $"CHAR({maxLen ?? 1})",
                    "ntext" or "text" => "TEXT",
                    "datetime" or "datetime2" => "DATETIME(6)",
                    "date" => "DATE",
                    "time" => "TIME",
                    "uniqueidentifier" => "VARCHAR(36)",
                    "varbinary" => maxLen == -1 ? "LONGBLOB" : $"VARBINARY({maxLen ?? 255})",
                    "float" => "DOUBLE",
                    "real" => "FLOAT",
                    _ => dataType.ToUpperInvariant()
                };
            }

            sb.AppendLine($"CREATE TABLE IF NOT EXISTS `{tableName}` (");
            var colLines = new List<string>();
            foreach (var col in columns)
            {
                var mysqlType = MapType(col.Name, col.DataType, col.MaxLen, col.NumericPrec, col.NumericScale);
                var nullable = col.IsNullable == "YES" ? "NULL" : "NOT NULL";
                var defaultVal = "";
                if (col.Default != null)
                {
                    var def = col.Default.Trim('(', ')');
                    if (def.StartsWith("'") || def.StartsWith("N'"))
                        defaultVal = $" DEFAULT {def.Replace("N'", "'").TrimEnd(')')}";
                    else if (decimal.TryParse(def, out _))
                        defaultVal = $" DEFAULT {def}";
                    else if (def.Equals("getdate()", StringComparison.OrdinalIgnoreCase) ||
                             def.Equals("getutcdate()", StringComparison.OrdinalIgnoreCase))
                        defaultVal = " DEFAULT CURRENT_TIMESTAMP";
                }
                colLines.Add($"  `{col.Name}` {mysqlType} {nullable}{defaultVal}");
            }
            if (pkColumns.Count > 0)
            {
                colLines.Add($"  PRIMARY KEY ({string.Join(", ", pkColumns.Select(c => $"`{c}`"))})");
            }
            sb.AppendLine(string.Join(",\n", colLines));
            sb.AppendLine($") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;");
        }

        /// <summary>
        /// 获取指定表的主键列名（仅支持单列主键；复合主键取第一列；无主键返回 null）
        /// </summary>
        private async Task<string?> GetPrimaryKeyColumnAsync(System.Data.Common.DbConnection conn, string tableName, CancellationToken ct)
        {
            try
            {
                // 使用方言提供的主键列查询 SQL（参数 @table），返回多行取首行。
                using var cmd = conn.CreateCommand();
                cmd.CommandText = _dialect.SqlGetPrimaryKeyColumns;
                var p = cmd.CreateParameter();
                p.ParameterName = "@table";
                p.Value = tableName;
                cmd.Parameters.Add(p);
                using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    return reader.IsDBNull(0) ? null : reader.GetString(0);
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 导出指定表的数据为 INSERT 语句
        /// </summary>
        public async Task<string> ExportTableDataAsync(List<string> tableNames, CancellationToken ct, int? rowLimit = null, bool orderByPkDesc = false)
        {
            if (tableNames == null || tableNames.Count == 0) return string.Empty;

            using var scope = _services.CreateScope();
            var conn = GetSqlSugarConnection(scope.ServiceProvider);
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("-- ============================================================");
            sb.AppendLine("-- 模块表数据导出（自动生成，请勿手动修改）");
            sb.AppendLine($"-- 导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            if (rowLimit.HasValue)
                sb.AppendLine($"-- 每表最多导出 {rowLimit.Value} 行{(orderByPkDesc ? "（按主键降序，优先保留最新数据）" : string.Empty)}");
            sb.AppendLine("-- ============================================================");
            sb.AppendLine();

            foreach (var tableName in tableNames)
            {
                try
                {
                    // 查询主键列（仅在需要按主键排序时）
                    string? pkColumn = null;
                    if (orderByPkDesc)
                    {
                        pkColumn = await GetPrimaryKeyColumnAsync(conn, tableName, ct);
                    }

                    using var cmd = conn.CreateCommand();
                    var quotedName = _dialect.QuoteIdentifier(tableName);
                    var quotedPk = !string.IsNullOrEmpty(pkColumn) ? _dialect.QuoteIdentifier(pkColumn) : null;
                    var orderClause = quotedPk != null ? $" ORDER BY {quotedPk} DESC" : string.Empty;

                    // SQL Server 的 OFFSET/FETCH 必须伴随 ORDER BY，同时 LIMIT 0 表达不同；
                    // 这里仅当有 rowLimit 时才产生 LIMIT 子句。BuildLimitClause(0, count) 同时适应 MySQL/PG。
                    var limitClause = rowLimit.HasValue
                        ? " " + _dialect.BuildLimitClause(0, rowLimit.Value)
                        : string.Empty;
                    cmd.CommandText = $"SELECT * FROM {quotedName}{orderClause}{limitClause}";
                    using var reader = await cmd.ExecuteReaderAsync(ct);

                    var fieldCount = reader.FieldCount;
                    var columnNames = new List<string>();
                    for (int i = 0; i < fieldCount; i++)
                        columnNames.Add(reader.GetName(i));

                    var hasRows = false;
                    while (await reader.ReadAsync(ct))
                    {
                        if (!hasRows)
                        {
                            sb.AppendLine($"-- 表: {tableName}");
                            hasRows = true;
                        }
                        var values = new List<string>();
                        for (int i = 0; i < fieldCount; i++)
                        {
                            if (reader.IsDBNull(i))
                            {
                                values.Add("NULL");
                            }
                            else
                            {
                                var val = reader.GetValue(i);
                                if (val is string s)
                                    values.Add($"'{s.Replace("'", "''").Replace("\\", "\\\\")}'");
                                else if (val is DateTime dt)
                                    values.Add($"'{dt:yyyy-MM-dd HH:mm:ss.ffffff}'");
                                else if (val is bool b)
                                    values.Add(b ? "1" : "0");
                                else if (val is byte[] bytes)
                                    values.Add($"0x{BitConverter.ToString(bytes).Replace("-", "")}");
                                else
                                    values.Add(val.ToString() ?? "NULL");
                            }
                        }
                        sb.AppendLine($"INSERT INTO {_dialect.QuoteIdentifier(tableName)} ({string.Join(", ", columnNames.Select(c => _dialect.QuoteIdentifier(c)))}) VALUES ({string.Join(", ", values)});");
                    }

                    if (hasRows)
                        sb.AppendLine();
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"-- 导出表 {tableName} 数据失败: {ex.Message}");
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 从数据库导出模块的完整菜单树，用于更新 install.json 中的 Menus 节
        /// </summary>
        public async Task<InstallSpec?> ExportMenuTreeAsync(string rootCode, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(rootCode)) return null;

            using var scope = _services.CreateScope();
            var conn = GetSqlSugarConnection(scope.ServiceProvider);
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);

            // 1. 查找根菜单
            long rootId;
            string? rootName, rootIcon, rootSupportedClients, rootRoute;
            int rootOrderNo;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT Id, Name, Icon, SupportedClients, Route, OrderNo FROM ginkgo_Sys_Menu WHERE Code = @code AND (IsDeleted = 0 OR IsDeleted IS NULL)";
                var p = cmd.CreateParameter(); p.ParameterName = "@code"; p.Value = rootCode; cmd.Parameters.Add(p);
                using var reader = await cmd.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct)) return null;
                rootId = reader.GetInt64(0);
                rootName = reader.IsDBNull(1) ? null : reader.GetString(1);
                rootIcon = reader.IsDBNull(2) ? null : reader.GetString(2);
                rootSupportedClients = reader.IsDBNull(3) ? null : reader.GetString(3);
                rootRoute = reader.IsDBNull(4) ? null : reader.GetString(4);
                rootOrderNo = reader.IsDBNull(5) ? 100 : reader.GetInt32(5);
            }

            // 2. 递归收集所有子菜单
            var allMenus = new List<(long Id, long ParentId, string Name, string Route, string Type, string? ItemMode, string? Icon, string? Url, string? Code, int OrderNo, bool Visible, string? WebRouteUrl, string? WebDisplayMode, string? SupportedClients, string? Resource, string? Method)>();
            var currentLevel = new List<long> { rootId };
            while (currentLevel.Count > 0)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT Id, ParentId, Name, Route, Type, ItemMode, Icon, Url, Code, OrderNo, Visible, WebRouteUrl, WebDisplayMode, SupportedClients, Resource, Method FROM ginkgo_Sys_Menu WHERE ParentId IN ({string.Join(",", currentLevel)}) AND (IsDeleted = 0 OR IsDeleted IS NULL) ORDER BY OrderNo";
                var nextLevel = new List<long>();
                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var id = reader.GetInt64(0);
                    nextLevel.Add(id);
                    allMenus.Add((
                        Id: id,
                        ParentId: reader.IsDBNull(1) ? 0 : reader.GetInt64(1),
                        Name: reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Route: reader.IsDBNull(3) ? "" : reader.GetString(3),
                        Type: reader.IsDBNull(4) ? "Menu" : reader.GetString(4),
                        ItemMode: reader.IsDBNull(5) ? null : reader.GetString(5),
                        Icon: reader.IsDBNull(6) ? null : reader.GetString(6),
                        Url: reader.IsDBNull(7) ? null : reader.GetString(7),
                        Code: reader.IsDBNull(8) ? null : reader.GetString(8),
                        OrderNo: reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                        Visible: reader.IsDBNull(10) ? true : reader.GetBoolean(10),
                        WebRouteUrl: reader.IsDBNull(11) ? null : reader.GetString(11),
                        WebDisplayMode: reader.IsDBNull(12) ? null : reader.GetString(12),
                        SupportedClients: reader.IsDBNull(13) ? null : reader.GetString(13),
                        Resource: reader.IsDBNull(14) ? null : reader.GetString(14),
                        Method: reader.IsDBNull(15) ? null : reader.GetString(15)
                    ));
                }
                currentLevel = nextLevel;
            }

            // 3. 构建 Code->Id 映射（用于解析 ParentCode）
            var idToCode = new Dictionary<long, string?>();
            idToCode[rootId] = rootCode;
            foreach (var m in allMenus)
            {
                if (!string.IsNullOrWhiteSpace(m.Code))
                    idToCode[m.Id] = m.Code;
            }

            // 4. 构建 MenuItemSpec 列表
            var menuItems = allMenus.Select(m => new MenuItemSpec
            {
                Name = m.Name,
                Route = m.Route,
                Type = m.Type,
                ItemMode = m.ItemMode,
                Icon = m.Icon,
                Url = m.Url,
                Code = m.Code,
                ParentCode = m.ParentId == rootId ? null : (idToCode.TryGetValue(m.ParentId, out var pc) ? pc : null),
                WebRouteUrl = m.WebRouteUrl,
                WebDisplayMode = m.WebDisplayMode,
                SupportedClients = m.SupportedClients,
                Hidden = !m.Visible,
                SortOrder = m.OrderNo,
                Resource = m.Resource,
                Method = m.Method
            }).ToList();

            return new InstallSpec
            {
                Menus = new MenusSpec
                {
                    RootCode = rootCode,
                    RootName = rootName,
                    RootIcon = rootIcon,
                    RootSupportedClients = rootSupportedClients,
                    Items = menuItems
                }
            };
        }

    }

