namespace Ginkgo.Domain.Settings;

/// <summary>
/// 插件配置在 ginkgo_Sys_Settings 表中的键名约定。
/// </summary>
public static class ModuleConfigKeys
{
    /// <summary>构建插件配置项的唯一键：{moduleId}:{configFile}:{itemName}</summary>
    public static string Build(string moduleId, string configFile, string itemName)
        => $"{moduleId}:{configFile}:{itemName}";

    /// <summary>从 Settings 键解析出配置项名称（需已知 moduleId 与 configFile）。</summary>
    public static bool TryParseItemName(string key, string moduleId, string configFile, out string itemName)
    {
        var prefix = $"{moduleId}:{configFile}:";
        if (key.StartsWith(prefix, StringComparison.Ordinal))
        {
            itemName = key[prefix.Length..];
            return true;
        }
        itemName = string.Empty;
        return false;
    }
}
