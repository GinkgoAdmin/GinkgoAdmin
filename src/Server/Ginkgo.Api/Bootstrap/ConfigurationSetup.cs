using System.Text.RegularExpressions;
using Ginkgo.Api.Bootstrap.Configuration;
using Serilog;

namespace Ginkgo.Api.Bootstrap;

/// <summary>
/// 配置加载与安装模式检测（从 Program.cs 提取）。
/// </summary>
public static class ConfigurationSetup
{
    /// <summary>
    /// 加载 .jsonc 配置文件（支持 // 和 /* */ 注释）。
    /// </summary>
    public static WebApplicationBuilder AddJsoncConfiguration(this WebApplicationBuilder builder)
    {
        try
        {
            var contentRoot = builder.Environment.ContentRootPath;
            var baseJsonc = Path.Combine(contentRoot, "appsettings.jsonc");
            var envJsonc = Path.Combine(contentRoot, $"appsettings.{builder.Environment.EnvironmentName}.jsonc");
            var s1 = LoadJsoncAsStream(baseJsonc);
            if (s1 != null) builder.Configuration.AddJsonStream(s1);
            var s2 = LoadJsoncAsStream(envJsonc);
            if (s2 != null) builder.Configuration.AddJsonStream(s2);
        }
        catch { }
        return builder;
    }

    /// <summary>
    /// 加载 resource/db.json 运行时数据库配置（安装完成后写入），优先于 appsettings.json。
    /// 文件按 JSONC 解析（支持 // 与 /* */ 注释、尾随逗号），便于运维直接在文件中维护中文注释；
    /// 保留 reloadOnChange，配置改动下个请求即可生效。
    /// </summary>
    public static WebApplicationBuilder AddDatabaseJsonConfiguration(this WebApplicationBuilder builder)
    {
        try
        {
            var resourceDir = Path.Combine(builder.Environment.ContentRootPath, "resource");
            var fallbackDir = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "..", "resource"));
            var dbJson1 = Path.Combine(resourceDir, "db.json");
            var dbJson2 = Path.Combine(fallbackDir, "db.json");
            Log.Information("[BOOT] ContentRootPath: {ContentRoot}", builder.Environment.ContentRootPath);
            Log.Information("[BOOT] Looking for db.json at: {Path} (exists: {Exists})", dbJson1, File.Exists(dbJson1));
            if (File.Exists(dbJson1))
            {
                Log.Information("[BOOT] Loading db.json (jsonc) from: {Path}", dbJson1);
                builder.Configuration.AddJsoncFile(dbJson1, optional: true, reloadOnChange: true);
            }
            else if (File.Exists(dbJson2))
            {
                Log.Information("[BOOT] Loading db.json (jsonc) from fallback: {Path}", dbJson2);
                builder.Configuration.AddJsoncFile(dbJson2, optional: true, reloadOnChange: true);
            }
            else
            {
                Log.Warning("[BOOT] WARNING: db.json not found at either location!");
            }
        }
        catch (Exception ex) { Log.Error("[BOOT] Error loading db.json: {Error}", ex.Message); }
        return builder;
    }

    /// <summary>
    /// 配置 Kestrel 自托管 URLs：优先环境变量 ASPNETCORE_URLS，否则使用配置中的 Urls。
    /// </summary>
    public static WebApplicationBuilder ConfigureKestrelUrls(this WebApplicationBuilder builder)
    {
        builder.WebHost.UseKestrel();
        var envUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
        var cfgUrls = builder.Configuration["Urls"];
        if (!string.IsNullOrWhiteSpace(envUrls))
        {
            builder.WebHost.UseUrls(envUrls);
        }
        else if (!string.IsNullOrWhiteSpace(cfgUrls))
        {
            builder.WebHost.UseUrls(cfgUrls);
        }
        return builder;
    }

    /// <summary>
    /// 检测安装模式（是否存在 install.lock 文件）。
    /// </summary>
    /// <returns>(installationMode, resourceDirToUse)</returns>
    public static (bool InstallationMode, string ResourceDir) DetectInstallationMode(this WebApplicationBuilder builder)
    {
        string resourceDirToUse = Path.Combine(builder.Environment.ContentRootPath, "resource");
        try
        {
            if (!Directory.Exists(resourceDirToUse))
            {
                var fallback = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "..", "resource"));
                if (Directory.Exists(fallback)) resourceDirToUse = fallback;
            }
        }
        catch { }
        var lockFile = Path.Combine(resourceDirToUse, "install.lock");
        var installationMode = !File.Exists(lockFile);
        Log.Information("[BOOT] Installation mode: {Mode} (lock: {LockFile})", installationMode ? "ON" : "OFF", lockFile);
        if (installationMode)
        {
            Log.Warning("[BOOT] 未检测到 install.lock，已进入安装模式：请在浏览器访问 /install 进行安装。");
        }
        return (installationMode, resourceDirToUse);
    }

    private static Stream? LoadJsoncAsStream(string path)
    {
        if (!File.Exists(path)) return null;
        var text = File.ReadAllText(path);
        text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        text = Regex.Replace(text, @"^\s*//.*?$", string.Empty, RegexOptions.Multiline);
        return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));
    }
}
