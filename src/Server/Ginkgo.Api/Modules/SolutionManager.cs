using System.Text;
using System.Text.RegularExpressions;

namespace Ginkgo.Api.Modules;

/// <summary>
/// 解决方案文件管理器 - 用于在模块安装/卸载时自动修改 .sln 文件
/// </summary>
public sealed class SolutionManager
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<SolutionManager> _logger;

    // C# 项目类型 GUID
    private const string CSharpProjectTypeGuid = "FAE04EC0-301F-11D3-BF4B-00C04F79EFBC";
    // 解决方案文件夹类型 GUID
    private const string SolutionFolderTypeGuid = "2150E333-8FDC-42A3-9474-1A3956D46DE8";

    public SolutionManager(IWebHostEnvironment env, ILogger<SolutionManager> logger)
    {
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// 读取文件并保留原始编码（BOM 等），防止写回时编码丢失导致乱码
    /// </summary>
    private static async Task<(string Content, Encoding Encoding)> ReadFilePreservingEncodingAsync(string path, CancellationToken ct)
    {
        byte[] head;
        await using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            head = new byte[Math.Min(4, (int)fs.Length)];
            _ = await fs.ReadAsync(head.AsMemory(0, head.Length), ct);
        }

        Encoding encoding;
        if (head.Length >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF)
            encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        else if (head.Length >= 2 && head[0] == 0xFF && head[1] == 0xFE)
            encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        else if (head.Length >= 2 && head[0] == 0xFE && head[1] == 0xFF)
            encoding = new UnicodeEncoding(bigEndian: true, byteOrderMark: true);
        else
            encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);
        var content = await reader.ReadToEndAsync(ct);
        return (content, encoding);
    }

    /// <summary>
    /// 检测文本中主要使用的换行符（CRLF 优先，其次 LF）
    /// </summary>
    private static string DetectNewline(string content)
    {
        var crlf = 0;
        var lf = 0;
        for (int i = 0; i < content.Length; i++)
        {
            if (content[i] == '\n')
            {
                if (i > 0 && content[i - 1] == '\r') crlf++;
                else lf++;
            }
        }
        return crlf >= lf ? "\r\n" : "\n";
    }

    /// <summary>
    /// 获取解决方案文件路径（优先查找 GinkgoAdmin.sln）
    /// </summary>
    private string? FindSolutionFile()
    {
        var searchDirs = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            Path.Combine(Directory.GetCurrentDirectory(), ".."),
            Path.Combine(Directory.GetCurrentDirectory(), "..", ".."),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".."),
            Path.Combine(AppContext.BaseDirectory, ".."),
            Path.Combine(AppContext.BaseDirectory, "..", ".."),
            Path.Combine(AppContext.BaseDirectory, "..", "..", ".."),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."),
        };

        foreach (var dir in searchDirs)
        {
            try
            {
                var fullPath = Path.GetFullPath(dir);
                var targetSln = Path.Combine(fullPath, "GinkgoAdmin.sln");
                if (File.Exists(targetSln))
                    return targetSln;
            }
            catch { }
        }

        foreach (var dir in searchDirs)
        {
            try
            {
                var fullPath = Path.GetFullPath(dir);
                var slnFiles = Directory.GetFiles(fullPath, "*.sln", SearchOption.TopDirectoryOnly);
                if (slnFiles.Length > 0)
                    return slnFiles[0];
            }
            catch { }
        }
        return null;
    }

    /// <summary>
    /// 获取模块短名称（用于生成唯一的文件夹名）
    /// </summary>
    private static string GetModuleShortName(string moduleId)
    {
        var parts = moduleId.Split('.');
        return parts.Length > 0 ? parts[^1] : moduleId;
    }

    /// <summary>
    /// 在 sln 中查找指定名称的解决方案文件夹 GUID（大写，不带花括号）
    /// </summary>
    private static string? FindSolutionFolderGuid(string slnContent, string folderName)
    {
        var pattern = new Regex(
            @"Project\(""\{" + Regex.Escape(SolutionFolderTypeGuid) + @"\}""\)\s*=\s*""" + Regex.Escape(folderName)
                + @"""\s*,\s*""" + Regex.Escape(folderName) + @"""\s*,\s*""\{([0-9A-Fa-f\-]{36})\}""",
            RegexOptions.IgnoreCase);
        var m = pattern.Match(slnContent);
        return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
    }

    /// <summary>
    /// 在独立一行的 Global 之前插入文本
    /// </summary>
    private static string InsertBeforeGlobal(string slnContent, string text, string newline)
    {
        var lfIdx = slnContent.IndexOf("\nGlobal\r\n", StringComparison.Ordinal);
        if (lfIdx < 0) lfIdx = slnContent.IndexOf("\nGlobal\n", StringComparison.Ordinal);
        if (lfIdx < 0) return slnContent;
        var insertAt = lfIdx + 1; // 落在 "Global" 开头
        return slnContent.Insert(insertAt, text);
    }

    /// <summary>
    /// 确保 src/Module 两层解决方案文件夹存在，返回 Module 文件夹的 GUID（大写，不带花括号）
    /// </summary>
    private static (string content, string moduleFolderGuid, bool modified) EnsureModuleFolder(string slnContent, string newline)
    {
        var modified = false;

        var srcGuid = FindSolutionFolderGuid(slnContent, "src");
        if (srcGuid == null)
        {
            srcGuid = GenerateDeterministicGuid("folder_root_src");
            var entry = $"Project(\"{{{SolutionFolderTypeGuid}}}\") = \"src\", \"src\", \"{{{srcGuid}}}\"" + newline
                      + "EndProject" + newline;
            slnContent = InsertBeforeGlobal(slnContent, entry, newline);
            modified = true;
        }

        var moduleGuid = FindSolutionFolderGuid(slnContent, "Module");
        if (moduleGuid == null)
        {
            moduleGuid = GenerateDeterministicGuid("folder_root_module");
            var entry = $"Project(\"{{{SolutionFolderTypeGuid}}}\") = \"Module\", \"Module\", \"{{{moduleGuid}}}\"" + newline
                      + "EndProject" + newline;
            slnContent = InsertBeforeGlobal(slnContent, entry, newline);
            slnContent = AddNestedProject(slnContent, moduleGuid, srcGuid, newline);
            modified = true;
        }

        return (slnContent, moduleGuid, modified);
    }

    /// <summary>
    /// 添加模块项目到解决方案
    /// </summary>
    public async Task<bool> AddModuleToSolutionAsync(string moduleId, string? serverCsprojPath, string? clientCsprojPath, string? contractsCsprojPath = null, CancellationToken ct = default)
    {
        var slnPath = FindSolutionFile();
        if (slnPath == null)
        {
            _logger.LogWarning("未找到解决方案文件，跳过添加模块项目");
            return false;
        }

        try
        {
            var (slnContent, slnEncoding) = await ReadFilePreservingEncodingAsync(slnPath, ct);
            var newline = DetectNewline(slnContent);
            var slnDir = Path.GetDirectoryName(slnPath)!;
            var modified = false;
            var shortName = GetModuleShortName(moduleId);

            var moduleFolderGuid = GenerateDeterministicGuid($"folder_{moduleId}");
            var serverFolderGuid = GenerateDeterministicGuid($"folder_{moduleId}_server");
            var clientFolderGuid = GenerateDeterministicGuid($"folder_{moduleId}_client");
            var contractsFolderGuid = GenerateDeterministicGuid($"folder_{moduleId}_contracts");

            // 确保 src/Module 两级解决方案文件夹存在
            var (updated, moduleRootFolderGuid, folderCreated) = EnsureModuleFolder(slnContent, newline);
            slnContent = updated;
            if (folderCreated) modified = true;

            // 1) 模块自身容器文件夹（如 Ginkgo.Module.AICore）及子文件夹
            if (FindSolutionFolderGuid(slnContent, moduleId) == null)
            {
                var folderEntry = $"Project(\"{{{SolutionFolderTypeGuid}}}\") = \"{moduleId}\", \"{moduleId}\", \"{{{moduleFolderGuid}}}\"" + newline + "EndProject" + newline;

                var serverFolderName = $"{shortName}.server";
                folderEntry += $"Project(\"{{{SolutionFolderTypeGuid}}}\") = \"{serverFolderName}\", \"{serverFolderName}\", \"{{{serverFolderGuid}}}\"" + newline + "EndProject" + newline;

                if (!string.IsNullOrEmpty(clientCsprojPath))
                {
                    var clientFolderName = $"{shortName}.client";
                    folderEntry += $"Project(\"{{{SolutionFolderTypeGuid}}}\") = \"{clientFolderName}\", \"{clientFolderName}\", \"{{{clientFolderGuid}}}\"" + newline + "EndProject" + newline;
                }

                if (!string.IsNullOrEmpty(contractsCsprojPath))
                {
                    var contractsFolderName = $"{shortName}.contracts";
                    folderEntry += $"Project(\"{{{SolutionFolderTypeGuid}}}\") = \"{contractsFolderName}\", \"{contractsFolderName}\", \"{{{contractsFolderGuid}}}\"" + newline + "EndProject" + newline;
                }

                slnContent = InsertBeforeGlobal(slnContent, folderEntry, newline);
                modified = true;
            }

            // 2) 添加服务端项目
            if (!string.IsNullOrEmpty(serverCsprojPath) && File.Exists(serverCsprojPath))
            {
                var relativePath = Path.GetRelativePath(Path.GetFullPath(slnDir), Path.GetFullPath(serverCsprojPath)).Replace('/', '\\');
                var projectName = Path.GetFileNameWithoutExtension(serverCsprojPath);
                var projectGuid = GenerateDeterministicGuid($"proj_{moduleId}_server");

                if (!slnContent.Contains($"\"{relativePath}\"", StringComparison.OrdinalIgnoreCase))
                {
                    var projectEntry = $"Project(\"{{{CSharpProjectTypeGuid}}}\") = \"{projectName}\", \"{relativePath}\", \"{{{projectGuid}}}\"" + newline + "EndProject" + newline;
                    slnContent = InsertBeforeGlobal(slnContent, projectEntry, newline);
                    slnContent = AddProjectConfiguration(slnContent, projectGuid, newline);
                    slnContent = AddNestedProject(slnContent, projectGuid, serverFolderGuid, newline);
                    modified = true;
                }
            }

            // 3) 添加客户端项目
            if (!string.IsNullOrEmpty(clientCsprojPath) && File.Exists(clientCsprojPath))
            {
                var relativePath = Path.GetRelativePath(Path.GetFullPath(slnDir), Path.GetFullPath(clientCsprojPath)).Replace('/', '\\');
                var projectName = Path.GetFileNameWithoutExtension(clientCsprojPath);
                var projectGuid = GenerateDeterministicGuid($"proj_{moduleId}_client");

                if (!slnContent.Contains($"\"{relativePath}\"", StringComparison.OrdinalIgnoreCase))
                {
                    var projectEntry = $"Project(\"{{{CSharpProjectTypeGuid}}}\") = \"{projectName}\", \"{relativePath}\", \"{{{projectGuid}}}\"" + newline + "EndProject" + newline;
                    slnContent = InsertBeforeGlobal(slnContent, projectEntry, newline);
                    slnContent = AddProjectConfiguration(slnContent, projectGuid, newline);
                    slnContent = AddNestedProject(slnContent, projectGuid, clientFolderGuid, newline);
                    modified = true;
                }
            }

            // 4) 添加契约项目
            if (!string.IsNullOrEmpty(contractsCsprojPath) && File.Exists(contractsCsprojPath))
            {
                var relativePath = Path.GetRelativePath(Path.GetFullPath(slnDir), Path.GetFullPath(contractsCsprojPath)).Replace('/', '\\');
                var projectName = Path.GetFileNameWithoutExtension(contractsCsprojPath);
                var projectGuid = GenerateDeterministicGuid($"proj_{moduleId}_contracts");

                if (!slnContent.Contains($"\"{relativePath}\"", StringComparison.OrdinalIgnoreCase))
                {
                    var projectEntry = $"Project(\"{{{CSharpProjectTypeGuid}}}\") = \"{projectName}\", \"{relativePath}\", \"{{{projectGuid}}}\"" + newline + "EndProject" + newline;
                    slnContent = InsertBeforeGlobal(slnContent, projectEntry, newline);
                    slnContent = AddProjectConfiguration(slnContent, projectGuid, newline);
                    slnContent = AddNestedProject(slnContent, projectGuid, contractsFolderGuid, newline);
                    modified = true;
                }
            }

            // 5) 文件夹嵌套关系
            if (modified)
            {
                slnContent = AddNestedProject(slnContent, moduleFolderGuid, moduleRootFolderGuid, newline);
                slnContent = AddNestedProject(slnContent, serverFolderGuid, moduleFolderGuid, newline);
                if (!string.IsNullOrEmpty(clientCsprojPath))
                    slnContent = AddNestedProject(slnContent, clientFolderGuid, moduleFolderGuid, newline);
                if (!string.IsNullOrEmpty(contractsCsprojPath))
                    slnContent = AddNestedProject(slnContent, contractsFolderGuid, moduleFolderGuid, newline);

                await File.WriteAllTextAsync(slnPath, slnContent, slnEncoding, ct);
                _logger.LogInformation("已将模块 {ModuleId} 添加到解决方案 {SlnPath}", moduleId, Path.GetFileName(slnPath));
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加模块到解决方案失败: {ModuleId}", moduleId);
            return false;
        }
    }

    /// <summary>
    /// 从解决方案移除模块项目（保留 src/Module 根文件夹）
    /// </summary>
    public async Task<bool> RemoveModuleFromSolutionAsync(string moduleId, CancellationToken ct = default)
    {
        var slnPath = FindSolutionFile();
        if (slnPath == null)
        {
            _logger.LogWarning("未找到解决方案文件，跳过移除模块项目");
            return false;
        }

        try
        {
            var (slnContent, slnEncoding) = await ReadFilePreservingEncodingAsync(slnPath, ct);
            var newline = DetectNewline(slnContent);
            var lines = slnContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
            var modified = false;

            // 只移除模块自身相关 GUID；保留 Module / src 根文件夹
            var moduleFolderGuid = GenerateDeterministicGuid($"folder_{moduleId}").ToUpperInvariant();
            var serverFolderGuid = GenerateDeterministicGuid($"folder_{moduleId}_server").ToUpperInvariant();
            var clientFolderGuid = GenerateDeterministicGuid($"folder_{moduleId}_client").ToUpperInvariant();
            var contractsFolderGuid = GenerateDeterministicGuid($"folder_{moduleId}_contracts").ToUpperInvariant();
            var serverProjectGuid = GenerateDeterministicGuid($"proj_{moduleId}_server").ToUpperInvariant();
            var clientProjectGuid = GenerateDeterministicGuid($"proj_{moduleId}_client").ToUpperInvariant();
            var contractsProjectGuid = GenerateDeterministicGuid($"proj_{moduleId}_contracts").ToUpperInvariant();

            var guidsToRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                moduleFolderGuid, serverFolderGuid, clientFolderGuid, contractsFolderGuid,
                serverProjectGuid, clientProjectGuid, contractsProjectGuid
            };

            var newLines = new List<string>();
            var skipUntilEndProject = false;
            foreach (var line in lines)
            {
                if (skipUntilEndProject)
                {
                    if (line.Trim() == "EndProject")
                        skipUntilEndProject = false;
                    modified = true;
                    continue;
                }

                var shouldRemove = guidsToRemove.Any(g => line.Contains($"{{{g}}}", StringComparison.OrdinalIgnoreCase));

                if (shouldRemove && line.TrimStart().StartsWith("Project("))
                {
                    skipUntilEndProject = true;
                    modified = true;
                    continue;
                }

                if (shouldRemove)
                {
                    modified = true;
                    continue;
                }

                newLines.Add(line);
            }

            if (modified)
            {
                await File.WriteAllTextAsync(slnPath, string.Join(newline, newLines), slnEncoding, ct);
                _logger.LogInformation("已从解决方案移除模块 {ModuleId}（保留 Module 根文件夹）", moduleId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从解决方案移除模块失败: {ModuleId}", moduleId);
            return false;
        }
    }

    /// <summary>
    /// 添加项目配置（Debug/Release）
    /// </summary>
    private static string AddProjectConfiguration(string slnContent, string projectGuid, string newline)
    {
        if (slnContent.Contains($"{{{projectGuid}}}.Debug|Any CPU.ActiveCfg", StringComparison.OrdinalIgnoreCase))
            return slnContent;

        var configSection = "GlobalSection(ProjectConfigurationPlatforms) = postSolution";
        var configIndex = slnContent.IndexOf(configSection, StringComparison.Ordinal);
        if (configIndex < 0) return slnContent;

        var endIndex = slnContent.IndexOf("EndGlobalSection", configIndex, StringComparison.Ordinal);
        if (endIndex < 0) return slnContent;

        var config = $"\t\t{{{projectGuid}}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU" + newline +
                     $"\t\t{{{projectGuid}}}.Debug|Any CPU.Build.0 = Debug|Any CPU" + newline +
                     $"\t\t{{{projectGuid}}}.Release|Any CPU.ActiveCfg = Release|Any CPU" + newline +
                     $"\t\t{{{projectGuid}}}.Release|Any CPU.Build.0 = Release|Any CPU" + newline;

        return slnContent.Insert(endIndex, config);
    }

    /// <summary>
    /// 添加嵌套关系（若 NestedProjects 节不存在则创建）
    /// </summary>
    private static string AddNestedProject(string slnContent, string childGuid, string parentGuid, string newline)
    {
        if (slnContent.Contains($"{{{childGuid}}} = {{{parentGuid}}}", StringComparison.OrdinalIgnoreCase))
            return slnContent;

        var nestedSection = "GlobalSection(NestedProjects) = preSolution";
        var nestedIndex = slnContent.IndexOf(nestedSection, StringComparison.Ordinal);
        if (nestedIndex < 0)
        {
            var endGlobalIdx = slnContent.LastIndexOf("EndGlobal", StringComparison.Ordinal);
            if (endGlobalIdx < 0) return slnContent;
            var section = "\t" + nestedSection + newline +
                          $"\t\t{{{childGuid}}} = {{{parentGuid}}}" + newline +
                          "\tEndGlobalSection" + newline;
            return slnContent.Insert(endGlobalIdx, section);
        }

        var endIndex = slnContent.IndexOf("EndGlobalSection", nestedIndex, StringComparison.Ordinal);
        if (endIndex < 0) return slnContent;

        var nested = $"\t\t{{{childGuid}}} = {{{parentGuid}}}" + newline;
        return slnContent.Insert(endIndex, nested);
    }

    /// <summary>
    /// 生成确定性 GUID（基于字符串）
    /// </summary>
    private static string GenerateDeterministicGuid(string input)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return new Guid(hash).ToString().ToUpperInvariant();
    }
}
