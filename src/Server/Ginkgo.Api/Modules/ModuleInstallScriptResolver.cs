using System.Text.Json.Nodes;

namespace Ginkgo.Api.Modules;

/// <summary>
/// 安装时按当前数据库方言解析 install.json 中应执行的 SQL 脚本列表。
/// </summary>
public static class ModuleInstallScriptResolver
{
    /// <summary>解析结果。</summary>
    public sealed record Resolution(IReadOnlyList<string> AbsolutePaths, IReadOnlyList<string> RelativePaths, bool ScriptsAreNativeDialect);

    /// <summary>
    /// 从 install.json 所在目录与安装规范解析当前方言应执行的 SQL 脚本绝对路径。
    /// 若找不到对应当前方言的脚本则抛出 <see cref="ModuleInstallSqlNotFoundException"/>。
    /// </summary>
    public static Resolution Resolve(string installJsonDir, string? currentProvider, ModuleSqlExecutor.InstallSpec? spec)
    {
        if (spec == null)
            throw new ModuleInstallSqlNotFoundException(currentProvider ?? "mysql", MapFolder(currentProvider), Array.Empty<string>());

        var dialectCode = NormalizeProviderCode(currentProvider);
        var dialectFolder = MapFolder(dialectCode);
        var relativeScripts = ResolveRelativeScripts(installJsonDir, spec, dialectCode, dialectFolder);

        if (relativeScripts.Count == 0)
        {
            throw new ModuleInstallSqlNotFoundException(
                dialectCode,
                dialectFolder,
                new[] { $"sql/{dialectFolder}/install.sql" });
        }

        var absolute = new List<string>(relativeScripts.Count);
        var missing = new List<string>();
        foreach (var rel in relativeScripts)
        {
            var path = Path.Combine(installJsonDir, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
                missing.Add(rel);
            else
                absolute.Add(path);
        }

        if (missing.Count > 0)
            throw new ModuleInstallSqlNotFoundException(dialectCode, dialectFolder, missing);

        var native = relativeScripts.Any(p => p.Contains($"sql/{dialectFolder}/", StringComparison.OrdinalIgnoreCase));
        return new Resolution(absolute, relativeScripts, native);
    }

    /// <summary>预检：验证当前方言 SQL 是否存在（上传/校验阶段）。</summary>
    public static string? ValidateOrGetError(string installJsonDir, string? currentProvider, InstallSpecLite? spec)
    {
        if (spec == null) return null;
        try
        {
            var executorSpec = new ModuleSqlExecutor.InstallSpec
            {
                SqlScripts = spec.SqlScripts,
                SqlScriptsByDialect = spec.SqlScriptsByDialect
            };
            Resolve(installJsonDir, currentProvider, executorSpec);
            return null;
        }
        catch (ModuleInstallSqlNotFoundException ex)
        {
            return ex.Message;
        }
    }

    private static List<string> ResolveRelativeScripts(
        string installJsonDir,
        ModuleSqlExecutor.InstallSpec spec,
        string dialectCode,
        string dialectFolder)
    {
        // 1. SqlScriptsByDialect 精确匹配
        if (spec.SqlScriptsByDialect != null &&
            spec.SqlScriptsByDialect.TryGetValue(dialectCode, out var byDialect) &&
            byDialect is { Length: > 0 })
        {
            return byDialect.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        }

        // 2. 兼容键名 mssql / postgresql / mysql
        if (spec.SqlScriptsByDialect != null)
        {
            foreach (var key in new[] { dialectFolder, dialectCode })
            {
                if (spec.SqlScriptsByDialect.TryGetValue(key, out var alt) && alt is { Length: > 0 })
                    return alt.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            }
        }

        // 3. 旧版 SqlScripts：若全部为当前方言目录或根目录脚本则沿用
        if (spec.SqlScripts is { Length: > 0 })
        {
            var legacy = spec.SqlScripts.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            var dialectOnly = legacy.Where(s =>
                s.Contains($"sql/{dialectFolder}/", StringComparison.OrdinalIgnoreCase)).ToList();
            if (dialectOnly.Count > 0)
                return dialectOnly;

            var hasAnyDialectFolder = legacy.Any(s =>
                s.Contains("sql/mysql/", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("sql/postgresql/", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("sql/mssql/", StringComparison.OrdinalIgnoreCase));
            if (!hasAnyDialectFolder)
                return legacy;
        }

        // 4. 自动发现 sql/{dialect}/install.sql (+ init_data.sql)
        var auto = new List<string>();
        var installPath = Path.Combine(installJsonDir, "sql", dialectFolder, "install.sql");
        if (File.Exists(installPath))
        {
            auto.Add($"sql/{dialectFolder}/install.sql");
            var initDataPath = Path.Combine(installJsonDir, "sql", dialectFolder, "init_data.sql");
            if (File.Exists(initDataPath))
                auto.Add($"sql/{dialectFolder}/init_data.sql");
            var iniDataPath = Path.Combine(installJsonDir, "sql", dialectFolder, "ini_data.sql");
            if (File.Exists(iniDataPath))
                auto.Add($"sql/{dialectFolder}/ini_data.sql");
            return auto;
        }

        return new List<string>();
    }

    /// <summary>将方言勾选列表写入 install.json 的 SqlScriptsByDialect 与 SqlScripts（当前方言）。</summary>
    public static async Task RegisterDialectScriptsInInstallJsonAsync(
        string installJsonPath,
        IReadOnlyList<string> dialectCodes,
        bool includeInitData,
        bool includeIniData,
        string? currentProvider,
        CancellationToken ct)
    {
        if (!File.Exists(installJsonPath)) return;

        var json = await File.ReadAllTextAsync(installJsonPath, ct);
        var node = JsonNode.Parse(json);
        if (node == null) return;

        var currentCode = NormalizeProviderCode(currentProvider);
        var byDialect = node["SqlScriptsByDialect"] as JsonObject ?? new JsonObject();
        JsonArray? currentScripts = null;

        foreach (var dialect in dialectCodes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var code = NormalizeProviderCode(dialect);
            var folder = MapFolder(code);
            var scripts = byDialect[code] as JsonArray ?? new JsonArray();

            void EnsureScript(string rel)
            {
                var exists = scripts.Any(s => string.Equals(s?.GetValue<string>(), rel, StringComparison.OrdinalIgnoreCase));
                if (!exists) scripts.Add(rel);
            }

            EnsureScript($"sql/{folder}/install.sql");
            if (includeInitData)
                EnsureScript($"sql/{folder}/init_data.sql");
            if (includeIniData)
                EnsureScript($"sql/{folder}/ini_data.sql");

            byDialect[code] = scripts;

            if (string.Equals(code, currentCode, StringComparison.OrdinalIgnoreCase))
                currentScripts = scripts;
        }

        node["SqlScriptsByDialect"] = byDialect;
        if (currentScripts != null)
            node["SqlScripts"] = currentScripts.DeepClone();

        await File.WriteAllTextAsync(
            installJsonPath,
            node.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
            System.Text.Encoding.UTF8,
            ct);
    }

    public static string NormalizeProviderCode(string? code)
        => (code ?? "mysql").Trim().ToLowerInvariant() switch
        {
            "pgsql" => "postgresql",
            "postgres" => "postgresql",
            "mssql" => "sqlserver",
            _ => (code ?? "mysql").Trim().ToLowerInvariant()
        };

    public static string MapFolder(string? dialectCode)
        => ModuleInstallSqlLocator.MapDialectCodeToSqlFolder(NormalizeProviderCode(dialectCode));

    /// <summary>上传/校验阶段使用的精简 InstallSpec。</summary>
    public sealed class InstallSpecLite
    {
        public string[]? SqlScripts { get; set; }
        public Dictionary<string, string[]>? SqlScriptsByDialect { get; set; }
    }
}
