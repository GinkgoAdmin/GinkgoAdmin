using System.Text.Json;

namespace Ginkgo.Api.Modules;

/// <summary>
/// 解析模块在前端端点中的实际安装目录。
/// </summary>
public static class ModulePluginDirectoryResolver
{
    private static readonly string[] ManifestFileNames =
    [
        "module.json",
        "plugin.json",
        "manifest.json"
    ];

    public static string ExtractShortName(string moduleId)
    {
        if (moduleId.StartsWith("Ginkgo.Module.", StringComparison.OrdinalIgnoreCase))
        {
            return moduleId["Ginkgo.Module.".Length..].ToLowerInvariant();
        }

        var parts = moduleId.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return (parts.LastOrDefault() ?? moduleId).ToLowerInvariant();
    }

    public static IReadOnlyList<string> FindPluginDirectories(string pluginsRoot, string moduleId)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var shortName = ExtractShortName(moduleId);

        AddIfExists(Path.Combine(pluginsRoot, shortName));

        if (Directory.Exists(pluginsRoot))
        {
            foreach (var dir in Directory.GetDirectories(pluginsRoot))
            {
                if (DirectoryMatchesModuleId(dir, moduleId))
                {
                    AddIfExists(dir);
                }
            }
        }

        return result;

        void AddIfExists(string dir)
        {
            if (!Directory.Exists(dir))
                return;

            var fullPath = Path.GetFullPath(dir);
            if (seen.Add(fullPath))
            {
                result.Add(fullPath);
            }
        }
    }

    private static bool DirectoryMatchesModuleId(string dir, string moduleId)
    {
        foreach (var fileName in ManifestFileNames)
        {
            var path = Path.Combine(dir, fileName);
            if (File.Exists(path) && ManifestMatchesModuleId(path, moduleId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ManifestMatchesModuleId(string manifestPath, string moduleId)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = doc.RootElement;

            return StringPropertyEquals(root, "id", moduleId)
                || StringPropertyEquals(root, "moduleId", moduleId)
                || StringPropertyEquals(root, "moduleID", moduleId)
                || StringPropertyEquals(root, "module", moduleId);
        }
        catch
        {
            return false;
        }
    }

    private static bool StringPropertyEquals(JsonElement element, string propertyName, string expected)
    {
        return element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
            && string.Equals(value.GetString(), expected, StringComparison.OrdinalIgnoreCase);
    }
}
