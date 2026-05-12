namespace Ginkgo.Shared;

/// <summary>
/// 模块配置文件路径统一解析工具。
/// 开发环境与生产部署使用同一套探测规则，所有插件模块应通过此工具定位自身的 config 文件，
/// 避免各模块各自实现不一致的路径探测逻辑。
///
/// <para>搜索优先级（首次命中即返回）：</para>
/// <list type="number">
/// <item>程序集位置（ALC 加载场景下最直接：bin/../config/、同级 config/ 等）</item>
/// <item>开发环境源码树（src/Module/{moduleId}/server/config/）</item>
/// <item>生产部署 modules/ 目录（modules/{moduleId}/{version}/server/config/）</item>
/// <item>{AppBaseDir}/config/{configFileName} 兜底</item>
/// </list>
/// </summary>
public static class ModuleConfigResolver
{
    /// <summary>
    /// 解析模块配置文件路径。模块 ID 自动从程序集名称推断。
    /// </summary>
    /// <param name="anchorType">模块内的任意类型（用于定位程序集目录）</param>
    /// <param name="configFileName">配置文件名（如 plugin-store.json、aicore.json）</param>
    /// <param name="fallbackToSample">找不到正式文件时是否尝试 .sample 文件</param>
    /// <returns>配置文件绝对路径（兜底路径可能不存在，调用者应自行检查 File.Exists）</returns>
    public static string Resolve(Type anchorType, string configFileName, bool fallbackToSample = false)
    {
        var moduleId = anchorType.Assembly.GetName().Name ?? "";
        return Resolve(anchorType, configFileName, moduleId, fallbackToSample);
    }

    /// <summary>
    /// 解析模块配置文件路径（显式指定模块 ID）。
    /// </summary>
    public static string Resolve(Type anchorType, string configFileName, string moduleId, bool fallbackToSample = false)
    {
        // 1. 程序集位置探测
        var found = ProbeFromAssembly(anchorType, configFileName, fallbackToSample);
        if (found != null) return found;

        // 2. 开发环境源码树
        found = ProbeDevSourceTree(moduleId, configFileName, fallbackToSample);
        if (found != null) return found;

        // 3. 生产部署 modules/ 目录
        found = ProbeProductionModules(moduleId, configFileName, fallbackToSample);
        if (found != null) return found;

        // 4. 兜底
        return Path.Combine(AppContext.BaseDirectory, "config", configFileName);
    }

    /// <summary>
    /// 获取所有已存在的配置文件路径（用于多目录同步写入场景，如管理界面保存配置时同步写入 dev + bin 等）。
    /// </summary>
    public static string[] ResolveAll(Type anchorType, string configFileName, bool fallbackToSample = false)
    {
        var moduleId = anchorType.Assembly.GetName().Name ?? "";
        return ResolveAll(anchorType, configFileName, moduleId, fallbackToSample);
    }

    /// <summary>
    /// 获取所有已存在的配置文件路径（显式指定模块 ID）。
    /// </summary>
    public static string[] ResolveAll(Type anchorType, string configFileName, string moduleId, bool fallbackToSample = false)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var asm = ProbeFromAssembly(anchorType, configFileName, fallbackToSample);
        if (asm != null) paths.Add(asm);

        // 开发环境可能有多个副本（源码 + bin 输出）
        foreach (var p in ProbeAllDevSourceTree(moduleId, configFileName, fallbackToSample))
            paths.Add(p);

        var prod = ProbeProductionModules(moduleId, configFileName, fallbackToSample);
        if (prod != null) paths.Add(prod);

        return paths.ToArray();
    }

    /// <summary>
    /// 解析模块的 server 目录路径（不针对具体文件，适用于需要扫描整个 server 目录的场景）。
    /// 返回包含 config/ 子目录或 module.json 的 server 级目录。
    /// </summary>
    public static string? ResolveServerDir(Type anchorType)
    {
        // 从程序集位置向上查找 server 级目录
        var asmLocation = anchorType.Assembly.Location;
        if (!string.IsNullOrEmpty(asmLocation))
        {
            var result = FindServerDir(Path.GetDirectoryName(asmLocation)!);
            if (result != null) return result;
        }

        // 开发环境源码树
        var moduleId = anchorType.Assembly.GetName().Name ?? "";
        var bases = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var b in bases)
        {
            var cur = new DirectoryInfo(b);
            for (var i = 0; i < 6 && cur != null; i++)
            {
                var probe = Path.Combine(cur.FullName, "src", "Module", moduleId, "server");
                if (Directory.Exists(probe)) return probe;
                cur = cur.Parent;
            }
        }

        // 生产部署
        var modulesRoot = Path.Combine(AppContext.BaseDirectory, "modules", moduleId);
        if (Directory.Exists(modulesRoot))
        {
            try
            {
                foreach (var versionDir in Directory.GetDirectories(modulesRoot).OrderByDescending(d => d))
                {
                    var serverDir = Path.Combine(versionDir, "server");
                    if (Directory.Exists(serverDir)) return serverDir;
                }
            }
            catch { }
        }

        return null;
    }

    #region 内部探测方法

    /// <summary>
    /// 从程序集位置向上查找 server 级目录（包含 config/ 子目录或 module.json）。
    /// </summary>
    private static string? FindServerDir(string startDir)
    {
        try
        {
            var di = new DirectoryInfo(startDir);
            for (int i = 0; i < 5 && di != null; i++)
            {
                if (Directory.Exists(Path.Combine(di.FullName, "config")))
                    return di.FullName;
                if (File.Exists(Path.Combine(di.FullName, "module.json")))
                    return di.FullName;
                di = di.Parent;
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// 从程序集物理位置探测配置文件。
    /// 生产部署目录结构：modules/{moduleId}/{version}/server/bin/xxx.dll
    /// 对应 config 位置：modules/{moduleId}/{version}/server/config/{configFileName}
    /// </summary>
    private static string? ProbeFromAssembly(Type anchorType, string configFileName, bool fallbackToSample)
    {
        var asmLocation = anchorType.Assembly.Location;
        if (string.IsNullOrEmpty(asmLocation)) return null;

        var asmDir = Path.GetDirectoryName(asmLocation)!;
        var probes = new[]
        {
            Path.Combine(asmDir, "config", configFileName),                                      // DLL 与 config/ 同级（DLL 在 server/ 下）
            Path.GetFullPath(Path.Combine(asmDir, "..", "config", configFileName)),               // DLL 在 server/bin/，config 在 server/config/
            Path.Combine(asmDir, "server", "config", configFileName),                             // DLL 在模块版本根目录
            Path.GetFullPath(Path.Combine(asmDir, "..", "server", "config", configFileName)),     // DLL 在版本根/bin/ 下
        };

        foreach (var p in probes)
        {
            if (File.Exists(p)) return p;
            if (fallbackToSample && File.Exists(p + ".sample")) return p + ".sample";
        }

        return null;
    }

    /// <summary>
    /// 在开发环境源码树中查找配置文件（首次命中）。
    /// </summary>
    private static string? ProbeDevSourceTree(string moduleId, string configFileName, bool fallbackToSample)
    {
        var bases = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var b in bases)
        {
            var cur = new DirectoryInfo(b);
            for (var i = 0; i < 6 && cur != null; i++)
            {
                var probe = Path.Combine(cur.FullName, "src", "Module", moduleId, "server", "config", configFileName);
                if (File.Exists(probe)) return probe;
                if (fallbackToSample && File.Exists(probe + ".sample")) return probe + ".sample";
                cur = cur.Parent;
            }
        }
        return null;
    }

    /// <summary>
    /// 在开发环境源码树中查找所有配置文件副本（含 bin 输出目录）。
    /// </summary>
    private static List<string> ProbeAllDevSourceTree(string moduleId, string configFileName, bool fallbackToSample)
    {
        var results = new List<string>();
        var bases = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var b in bases)
        {
            var cur = new DirectoryInfo(b);
            for (var i = 0; i < 6 && cur != null; i++)
            {
                var serverDir = Path.Combine(cur.FullName, "src", "Module", moduleId, "server");
                if (Directory.Exists(serverDir))
                {
                    // 源码 config 目录
                    var probe = Path.Combine(serverDir, "config", configFileName);
                    if (File.Exists(probe)) results.Add(probe);
                    else if (fallbackToSample && File.Exists(probe + ".sample")) results.Add(probe + ".sample");

                    // bin 目录下的副本
                    var binDir = Path.Combine(serverDir, "bin");
                    if (Directory.Exists(binDir))
                    {
                        try
                        {
                            foreach (var cfgDir in Directory.GetDirectories(binDir, "config", SearchOption.AllDirectories))
                            {
                                var binProbe = Path.Combine(cfgDir, configFileName);
                                if (File.Exists(binProbe)) results.Add(binProbe);
                            }
                        }
                        catch { }
                    }
                }
                cur = cur.Parent;
            }
        }
        return results;
    }

    /// <summary>
    /// 在生产部署 modules/ 目录中查找配置文件。
    /// 目录结构：{AppBaseDir}/modules/{moduleId}/{version}/server/config/{configFileName}
    /// </summary>
    private static string? ProbeProductionModules(string moduleId, string configFileName, bool fallbackToSample)
    {
        var modulesRoot = Path.Combine(AppContext.BaseDirectory, "modules", moduleId);
        if (!Directory.Exists(modulesRoot)) return null;

        try
        {
            foreach (var versionDir in Directory.GetDirectories(modulesRoot).OrderByDescending(d => d))
            {
                var probe = Path.Combine(versionDir, "server", "config", configFileName);
                if (File.Exists(probe)) return probe;
                if (fallbackToSample && File.Exists(probe + ".sample")) return probe + ".sample";
            }
        }
        catch { }

        return null;
    }

    #endregion
}
