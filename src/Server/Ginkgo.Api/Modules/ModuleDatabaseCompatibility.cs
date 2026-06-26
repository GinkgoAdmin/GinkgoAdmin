using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Ginkgo.Api.Modules;

/// <summary>
/// 模块与当前数据库方言的兼容策略。
/// 用于在 MySQL 等非 PostgreSQL 环境下自动跳过仅支持 PostgreSQL 的插件（如向量知识库）。
/// </summary>
public static class ModuleDatabaseCompatibility
{
    /// <summary>向量知识库模块 ID（PostgreSQL + pgvector 专用）。</summary>
    public const string KnowledgeModuleId = "Ginkgo.Module.Knowledge";

    /// <summary>当前连接是否为 PostgreSQL。</summary>
    public static bool IsPostgreSqlProvider(IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "MySql";
        return provider.Contains("postgre", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>当前环境下是否应加载/启用指定模块。</summary>
    public static bool ShouldLoadModule(string moduleId, IConfiguration configuration, string? moduleBaseDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(moduleId)) return true;
        if (!RequiresPostgreSql(moduleId, moduleBaseDirectory)) return true;
        return IsPostgreSqlProvider(configuration);
    }

    /// <summary>过滤出当前数据库环境下可加载的模块 ID。</summary>
    public static List<string> FilterLoadableModuleIds(IEnumerable<string> moduleIds, IConfiguration configuration)
    {
        return moduleIds
            .Where(id => ShouldLoadModule(id, configuration))
            .ToList();
    }

    /// <summary>菜单树中是否应展示指定模块归属的菜单（仅用于内存过滤，不可放入 SqlSugar/IQueryable Where）。</summary>
    public static bool IsModuleMenuVisible(string? moduleId, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(moduleId) || string.Equals(moduleId, "sys", StringComparison.OrdinalIgnoreCase))
            return true;
        return ShouldLoadModule(moduleId, configuration);
    }

    /// <summary>在内存中过滤与当前数据库不兼容的插件菜单项。</summary>
    public static List<T> FilterMenusInMemory<T>(IEnumerable<T> menus, IConfiguration configuration, Func<T, string?> moduleSelector)
    {
        return menus
            .Where(m => IsModuleMenuVisible(moduleSelector(m), configuration))
            .ToList();
    }

    /// <summary>判断模块是否声明为 PostgreSQL 专用。</summary>
    public static bool RequiresPostgreSql(string moduleId, string? moduleBaseDirectory = null)
    {
        if (string.Equals(moduleId, KnowledgeModuleId, StringComparison.OrdinalIgnoreCase))
            return true;

        var installPath = ResolveInstallJsonPath(moduleId, moduleBaseDirectory);
        if (installPath == null) return false;

        var spec = ModuleSqlExecutor.ReadInstallJson(installPath);
        return spec != null && InstallSpecRequiresPostgreSql(spec);
    }

    private static bool InstallSpecRequiresPostgreSql(ModuleSqlExecutor.InstallSpec spec)
    {
        if (spec.RequirePostgreSql)
            return true;

        if (spec.Config != null)
        {
            foreach (var kv in spec.Config)
            {
                if (ConfigNodeRequiresPostgreSql(kv.Value))
                    return true;
            }
        }

        return InferPostgreSqlOnlyFromSqlScripts(spec);
    }

    /// <summary>install.json 仅声明 postgresql 脚本、未声明 mysql 时视为 PG 专用。</summary>
    private static bool InferPostgreSqlOnlyFromSqlScripts(ModuleSqlExecutor.InstallSpec spec)
    {
        if (spec.SqlScriptsByDialect == null || spec.SqlScriptsByDialect.Count == 0)
            return false;

        var hasPg = spec.SqlScriptsByDialect.Keys.Any(k => k.Contains("postgre", StringComparison.OrdinalIgnoreCase));
        var hasMysql = spec.SqlScriptsByDialect.Keys.Any(k => k.Contains("mysql", StringComparison.OrdinalIgnoreCase));
        return hasPg && !hasMysql;
    }

    private static bool ConfigNodeRequiresPostgreSql(object? node)
    {
        switch (node)
        {
            case JsonElement je when je.ValueKind == JsonValueKind.Object:
                foreach (var prop in je.EnumerateObject())
                {
                    if (prop.Name.Equals("RequirePostgreSql", StringComparison.OrdinalIgnoreCase)
                        && prop.Value.ValueKind == JsonValueKind.True)
                        return true;
                    if (ConfigNodeRequiresPostgreSql(prop.Value))
                        return true;
                }
                return false;
            case Dictionary<string, object> dict:
                foreach (var kv in dict)
                {
                    if (kv.Key.Equals("RequirePostgreSql", StringComparison.OrdinalIgnoreCase)
                        && kv.Value is bool enabled && enabled)
                        return true;
                    if (ConfigNodeRequiresPostgreSql(kv.Value))
                        return true;
                }
                return false;
            case JsonElement nested when nested.ValueKind != JsonValueKind.Object:
                return false;
            default:
                return false;
        }
    }

    private static string? ResolveInstallJsonPath(string moduleId, string? moduleBaseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(moduleBaseDirectory))
        {
            var direct = Path.Combine(moduleBaseDirectory, "install.json");
            if (File.Exists(direct)) return direct;

            var serverPath = Path.Combine(moduleBaseDirectory, "server", "install.json");
            if (File.Exists(serverPath)) return serverPath;
        }

        var modulesRoot = Path.Combine(AppContext.BaseDirectory, "modules", moduleId);
        if (Directory.Exists(modulesRoot))
        {
            foreach (var versionDir in Directory.EnumerateDirectories(modulesRoot).OrderByDescending(Directory.GetCreationTimeUtc))
            {
                var versioned = Path.Combine(versionDir, "server", "install.json");
                if (File.Exists(versioned)) return versioned;
            }

            var legacy = Path.Combine(modulesRoot, "install.json");
            if (File.Exists(legacy)) return legacy;
        }

        var searchBases = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var baseDir in searchBases)
        {
            var current = new DirectoryInfo(baseDir);
            for (var i = 0; i < 8 && current != null; i++, current = current.Parent!)
            {
                var devPath = Path.Combine(current.FullName, "src", "Module", moduleId, "server", "install.json");
                if (File.Exists(devPath)) return devPath;
            }
        }

        return null;
    }
}
