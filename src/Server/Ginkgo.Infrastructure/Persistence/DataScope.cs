using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using SqlSugar;
using Ginkgo.ServerToolkit; // ICurrentUser
using Ginkgo.Domain.Settings; // ISettingsRepository

namespace Ginkgo.Infrastructure.Persistence;

/// <summary>
/// 数据范围解析器实现（基于配置、当前用户角色与部门树计算可访问集合）。
/// 接口定义（IDataScopeResolver）与 DTO（EffectiveDataScope / DataScopeOptions / DataScopeType）
/// 位于 Ginkgo.Plugin.Abstractions，供模块直接引用。
///
/// 配置优先级（从低到高）：
/// 1. appsettings.json 的 DataScope 节（兜底默认）
/// 2. DB Settings 表的 DataPermission.* 键（管理员后台 UI 写入，覆盖 1）
/// 3. DB Roles 表的 dataScope 字段（角色级配置，覆盖 1/2 中的 RoleStrategies）
///
/// 注意：本类被注册为 Scoped，请求级缓存避免重复查 DB。
/// </summary>
public sealed class DataScopeProvider : IDataScopeResolver
{
    private readonly IOptionsMonitor<DataScopeOptions> _opt;
    private readonly ISqlSugarClient _db;
    private readonly ICurrentUser _current;
    private readonly ISettingsRepository? _settings;

    // 请求级缓存：避免同一请求内重复查 DB
    private bool? _isEnabledCache;
    private EffectiveDataScope? _effectiveCache;

    /// <summary>
    /// 兼容旧构造函数（部分内部代码以 new DataScopeProvider 形式实例化时不传 ISettingsRepository）。
    /// </summary>
    public DataScopeProvider(IOptionsMonitor<DataScopeOptions> opt, ISqlSugarClient db, ICurrentUser current)
        : this(opt, db, current, null)
    { }

    /// <summary>
    /// 推荐构造函数：注入 ISettingsRepository 后即可读取 DB Settings 表覆盖 appsettings 配置。
    /// </summary>
    public DataScopeProvider(IOptionsMonitor<DataScopeOptions> opt, ISqlSugarClient db, ICurrentUser current, ISettingsRepository? settings)
    { _opt = opt; _db = db; _current = current; _settings = settings; }

    /// <inheritdoc />
    public EffectiveDataScope Resolve() => GetEffectiveScope();

    /// <summary>
    /// 数据范围过滤是否启用（供 SqlSugarRepository.Query() 等基础设施判断）。
    /// 优先读 DB Settings 的 DataPermission.Enabled；未配置时回退到 appsettings 的 DataScope:Enabled。
    /// </summary>
    public bool IsEnabled()
    {
        if (_isEnabledCache.HasValue) return _isEnabledCache.Value;
        bool enabled = _opt.CurrentValue?.Enabled ?? false;
        // DB 设置覆盖：DataPermission.Enabled
        var dbVal = TryGetSetting("DataPermission.Enabled");
        if (!string.IsNullOrWhiteSpace(dbVal) && bool.TryParse(dbVal.Trim(), out var parsed))
        {
            enabled = parsed;
        }
        _isEnabledCache = enabled;
        return enabled;
    }

    /// <summary>
    /// 入口：根据当前用户与配置计算有效范围（用于运行时），内部复用可单测的 BuildEffectiveScope。
    /// 实际执行顺序：
    /// 1. 用 appsettings 的 DataScopeOptions 作为基础
    /// 2. 用 DB Settings 中 DataPermission.DefaultScope 覆盖 DefaultStrategy（若存在）
    /// 3. 当前用户每个角色 code，先查 DB Roles 表的 dataScope 字段；若没记录则回退到 appsettings.RoleStrategies；都没有则用 DefaultStrategy
    /// 4. SpecifiedDepartments 类型：先查 RoleDataScopeDept 关联表；找不到则回退到 appsettings.RoleSpecifiedDepartments
    /// </summary>
    public EffectiveDataScope GetEffectiveScope()
    {
        if (_effectiveCache != null) return _effectiveCache;

        var cfg = _opt.CurrentValue ?? new DataScopeOptions();

        // 第 2 步：DB Settings.DataPermission.DefaultScope 覆盖 DefaultStrategy
        var defaultStrategy = cfg.DefaultStrategy;
        var dbDefault = TryGetSetting("DataPermission.DefaultScope");
        if (!string.IsNullOrWhiteSpace(dbDefault))
        {
            defaultStrategy = NormalizeScope(dbDefault, cfg.DefaultStrategy);
        }

        var roles = _current.Roles ?? Array.Empty<string>();
        var myDept = GetMyDepartmentIds();
        var myDeep = GetMyDepartmentAndChildrenIds();

        // 第 3 步：构建角色 -> 策略映射（DB Roles 优先）
        var dbRoleMap = QueryDbRoleStrategies(roles);
        var roleMap = roles.Select(r =>
        {
            DataScopeType type;
            if (dbRoleMap.TryGetValue(r, out var dbType))
            {
                type = dbType;
            }
            else if (cfg.RoleStrategies.TryGetValue(r, out var t))
            {
                type = t;
            }
            else
            {
                type = defaultStrategy;
            }
            return (r, type);
        });

        // 第 4 步：构建角色 -> 指定部门映射（DB RoleDataScopeDept 优先）
        var dbSpecMap = QueryDbRoleSpecifiedDepartments(roles, dbRoleMap);
        var specified = roles.ToDictionary(r => r, r =>
        {
            if (dbSpecMap.TryGetValue(r, out var ids) && ids.Count > 0)
                return (IReadOnlyCollection<long>)ids;
            if (cfg.RoleSpecifiedDepartments.TryGetValue(r, out var idsCfg))
                return (IReadOnlyCollection<long>)idsCfg;
            return Array.Empty<long>();
        });

        var result = BuildEffectiveScope(roles, cfg.AdminRoles, roleMap, myDept, myDeep, specified);
        _effectiveCache = result;
        return result;
    }

    /// <summary>
    /// 将外部输入的数据范围字符串规范化为 DataScopeType，兼容历史值（Self/Dept/DeptAndChildren）。
    /// 未识别值回退到 fallback。
    /// </summary>
    public static DataScopeType NormalizeScope(string? raw, DataScopeType fallback)
    {
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        var v = raw.Trim();
        // 大小写不敏感匹配
        if (string.Equals(v, "All", StringComparison.OrdinalIgnoreCase)) return DataScopeType.All;
        if (string.Equals(v, "OwnOnly", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v, "Self", StringComparison.OrdinalIgnoreCase))
            return DataScopeType.OwnOnly;
        if (string.Equals(v, "DepartmentOnly", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v, "Dept", StringComparison.OrdinalIgnoreCase))
            return DataScopeType.DepartmentOnly;
        if (string.Equals(v, "DepartmentAndChildren", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v, "DeptAndChildren", StringComparison.OrdinalIgnoreCase))
            return DataScopeType.DepartmentAndChildren;
        if (string.Equals(v, "SpecifiedDepartments", StringComparison.OrdinalIgnoreCase))
            return DataScopeType.SpecifiedDepartments;
        if (string.Equals(v, "Custom", StringComparison.OrdinalIgnoreCase))
            return DataScopeType.Custom;
        return fallback;
    }

    /// <summary>读 DB Settings 表中的某个 Key（失败时返回 null，不抛异常）。</summary>
    private string? TryGetSetting(string key)
    {
        if (_settings == null) return null;
        try
        {
            // 同步阻塞读取（DataScopeProvider 接口本身是同步的）；考虑到该方法每请求至多调用 2 次（Enabled + DefaultScope），
            // 配合本类实例缓存影响很小。
            var entity = _settings.GetAsync(key, null).GetAwaiter().GetResult();
            return entity?.Value;
        }
        catch { return null; }
    }

    /// <summary>
    /// 查询 DB Roles 表中给定角色 code 集合对应的 dataScope 字段，返回 RoleCode -> DataScopeType 映射。
    /// </summary>
    private Dictionary<string, DataScopeType> QueryDbRoleStrategies(IReadOnlyList<string> roleCodes)
    {
        var map = new Dictionary<string, DataScopeType>(StringComparer.OrdinalIgnoreCase);
        if (roleCodes == null || roleCodes.Count == 0) return map;
        try
        {
            var arr = roleCodes.ToArray();
            var rows = _db.Queryable<Ginkgo.Domain.Roles.Role>()
                          .Where(r => arr.Contains(r.Code))
                          .Select(r => new { r.Code, r.DataScope })
                          .ToList();
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Code)) continue;
                map[row.Code] = NormalizeScope(row.DataScope, DataScopeType.OwnOnly);
            }
        }
        catch { /* DB 异常不影响主流程，回退到 appsettings 的策略 */ }
        return map;
    }

    /// <summary>
    /// 查询 DB RoleDataScopeDept 表中给定角色 code 对应的部门列表。
    /// 仅对策略为 SpecifiedDepartments 的角色查询，避免无意义 IO。
    /// </summary>
    private Dictionary<string, List<long>> QueryDbRoleSpecifiedDepartments(
        IReadOnlyList<string> roleCodes,
        IReadOnlyDictionary<string, DataScopeType> dbRoleMap)
    {
        var result = new Dictionary<string, List<long>>(StringComparer.OrdinalIgnoreCase);
        if (roleCodes == null || roleCodes.Count == 0) return result;
        var specCodes = roleCodes.Where(c => dbRoleMap.TryGetValue(c, out var t) && t == DataScopeType.SpecifiedDepartments)
                                 .Distinct(StringComparer.OrdinalIgnoreCase)
                                 .ToArray();
        if (specCodes.Length == 0) return result;
        try
        {
            // 一次 JOIN 查询取出 (Code, DepartmentId) 列表
            var rows = _db.Queryable<Ginkgo.Domain.Roles.Role, Ginkgo.Domain.Roles.RoleDataScopeDept>(
                            (r, d) => new JoinQueryInfos(
                                JoinType.Inner, r.Id == d.RoleId
                            ))
                          .Where((r, d) => specCodes.Contains(r.Code))
                          .Select((r, d) => new { r.Code, d.DepartmentId })
                          .ToList();
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Code)) continue;
                if (!result.TryGetValue(row.Code, out var list))
                {
                    list = new List<long>();
                    result[row.Code] = list;
                }
                if (row.DepartmentId != 0 && !list.Contains(row.DepartmentId)) list.Add(row.DepartmentId);
            }
        }
        catch { /* DB 异常不影响主流程，回退到 appsettings 配置 */ }
        return result;
    }

    /// <summary>
    /// 纯函数：将角色策略与部门集整合为有效数据范围，便于单元测试。
    /// </summary>
    internal static EffectiveDataScope BuildEffectiveScope(
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> adminRoles,
        IEnumerable<(string Role, DataScopeType Type)> roleStrategies,
        IEnumerable<long> myDepartments,
        IEnumerable<long> myDepartmentsDeep,
        IReadOnlyDictionary<string, IReadOnlyCollection<long>> roleSpecified)
    {
        var res = new EffectiveDataScope();
        if (roles.Any(r => adminRoles.Contains(r))) { res.IsAll = true; return res; }
        var list = roleStrategies.ToList();
        if (list.Any(x => x.Type == DataScopeType.All)) { res.IsAll = true; return res; }
        foreach (var (role, type) in list)
        {
            switch (type)
            {
                case DataScopeType.OwnOnly:
                    res.IncludeOwn = true; break;
                case DataScopeType.DepartmentOnly:
                    foreach (var id in myDepartments) res.DepartmentIds.Add(id);
                    break;
                case DataScopeType.DepartmentAndChildren:
                    foreach (var id in myDepartmentsDeep) res.DepartmentIds.Add(id);
                    break;
                case DataScopeType.SpecifiedDepartments:
                    if (roleSpecified.TryGetValue(role, out var ids))
                        foreach (var id in ids) res.DepartmentIds.Add(id);
                    break;
            }
        }
        return res;
    }

    private IEnumerable<long> GetMyDepartmentIds()
    {
        var uid = _current.Id;
        if (uid == null) return Enumerable.Empty<long>();
        return _db.Queryable<Ginkgo.Domain.Users.UserDepartment>()
                  .Where(x => x.UserId == uid)
                  .Select(x => x.DepartmentId)
                  .ToList();
    }

    private IEnumerable<long> GetMyDepartmentAndChildrenIds()
    {
        var mine = GetMyDepartmentIds().ToHashSet();
        if (mine.Count == 0) return mine;
        var all = _db.Queryable<Ginkgo.Domain.Departments.Department>()
                     .Select(d => new { d.Id, d.ParentId })
                     .ToList();
        var map = all.Where(x => x.ParentId != null)
                     .GroupBy(x => x.ParentId!.Value)
                     .ToDictionary(g => g.Key, g => g.Select(v => v.Id).ToList());
        var q = new Queue<long>(mine);
        while (q.Count > 0)
        {
            var id = q.Dequeue();
            if (map.TryGetValue(id, out var children))
            {
                foreach (var c in children)
                    if (mine.Add(c)) q.Enqueue(c);
            }
        }
        return mine;
    }
}
