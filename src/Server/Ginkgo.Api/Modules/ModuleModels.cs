using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ginkgo.Api.Modules;

public sealed class ModuleManifest
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("version")] public string Version { get; set; } = "0.0.0";
    [JsonPropertyName("minAppVersion")] public string? MinAppVersion { get; set; }
    [JsonPropertyName("minDbSchemaVersion")] public string? MinDbSchemaVersion { get; set; }
    [JsonPropertyName("dependencies")] public string[]? Dependencies { get; set; }
    [JsonPropertyName("hasClient")] public bool HasClient { get; set; }
    [JsonPropertyName("hasPages")] public bool HasPages { get; set; }
    [JsonPropertyName("publisher")] public string? Publisher { get; set; }
    [JsonPropertyName("author")] public string? Author { get; set; }
    [JsonPropertyName("homepage")] public string? Homepage { get; set; }
    [JsonPropertyName("testRoute")] public string? TestRoute { get; set; }
    [JsonPropertyName("files")] public ModuleFile[]? Files { get; set; }
    [JsonPropertyName("signature")] public string? Signature { get; set; }
    [JsonPropertyName("capabilities")] public string[]? Capabilities { get; set; }
    [JsonPropertyName("channel")] public string? Channel { get; set; }
    [JsonPropertyName("tablePrefix")] public string? TablePrefix { get; set; }
    [JsonPropertyName("server")] public ServerConfig? Server { get; set; }
    [JsonPropertyName("client")] public ClientConfig? Client { get; set; }
    /// <summary>
    /// 插件配置存储方式：file（默认，写入 server/config/*.json）或 database（写入 ginkgo_Sys_Settings）。
    /// </summary>
    [JsonPropertyName("config")] public ModuleConfigOptions? Config { get; set; }
}

/// <summary>插件配置存储选项（module.json 中的 config 段）。</summary>
public sealed class ModuleConfigOptions
{
    /// <summary>存储方式：file | database，默认 file。</summary>
    [JsonPropertyName("storage")] public string Storage { get; set; } = "file";
    /// <summary>主配置文件名（如 aicore.json），storage=database 时用于键名前缀与 UI 元数据来源。</summary>
    [JsonPropertyName("primaryFile")] public string? PrimaryFile { get; set; }

    public bool IsDatabaseStorage =>
        string.Equals(Storage, "database", StringComparison.OrdinalIgnoreCase);
}

public sealed class ModuleFile
{
    [JsonPropertyName("path")] public string Path { get; set; } = string.Empty;
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = string.Empty;
}

public sealed class ServerConfig
{
    [JsonPropertyName("entryAssembly")] public string? EntryAssembly { get; set; }
    [JsonPropertyName("installScripts")] public string[]? InstallScripts { get; set; }
    [JsonPropertyName("uninstallScripts")] public string[]? UninstallScripts { get; set; }
}

public sealed class ClientConfig
{
    [JsonPropertyName("entryAssembly")] public string? EntryAssembly { get; set; }
}

public sealed class InstalledModule
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool HasClient { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime InstalledAtUtc { get; set; } = DateTime.Now;
    public string? Publisher { get; set; }
    public string? Homepage { get; set; }
    /// <summary>
    /// 插件菜单根编码（来自 install.json 的 Menus.RootCode），卸载时用于定位并移除关联菜单
    /// </summary>
    public string? MenuRootCode { get; set; }
}

/// <summary>
/// 增强的模块信息，合并了 module.json 文件和数据库中的信息
/// </summary>
public sealed class EnhancedModuleInfo
{
    // 数据库中的运行时信息
    public string Id { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTime InstalledAtUtc { get; set; } = DateTime.Now;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // module.json 中的静态元数据（优先级高于数据库）
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool HasClient { get; set; }
    public string? Publisher { get; set; }
    public string? Homepage { get; set; }
    public string? Author { get; set; }
    public string? Title { get; set; }
    public string? MinAppVersion { get; set; }
    public string[]? Dependencies { get; set; }
    public bool HasPages { get; set; }
    public string? TestRoute { get; set; }

    // 环境和路径信息
    public bool IsDevMode { get; set; }
    public string? ManifestPath { get; set; }

    // 运行时健康快照（来自 IModuleAppService.GetStatusAsync，writeLog=false 批量填充）
    // 用于前端列表上直接展示「红/绿灯」健康指示和「菜单注册」可见性，避免逐条额外请求 status 接口。
    /// <summary>运行时是否已加载（IModuleRuntimeQuery.IsLoaded）</summary>
    public bool RuntimeLoaded { get; set; }
    /// <summary>服务端 DLL 是否已落盘</summary>
    public bool ServerDllLoaded { get; set; }
    /// <summary>install.json 中是否声明了菜单（即 Menus.RootCode 非空）</summary>
    public bool HasMenus { get; set; }
    /// <summary>菜单是否已注册到 ginkgo_Sys_Menu（仅在 HasMenus=true 时有意义）</summary>
    public bool MenuRegistered { get; set; }

    /// <summary>插件配置存储方式：file | database（来自 module.json config.storage）</summary>
    public string? ConfigStorage { get; set; }
    /// <summary>数据库存储模式下的主配置文件名（来自 module.json config.primaryFile）</summary>
    public string? ConfigPrimaryFile { get; set; }
}

public sealed class ClientModuleTask
{
    public string ClientId { get; set; } = string.Empty; // 可为 "*" 代表全部客户端
    public string ModuleId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Action { get; set; } = "install"; // install/upgrade/uninstall
    public DateTime CreatedAtUtc { get; set; } = DateTime.Now;
}
