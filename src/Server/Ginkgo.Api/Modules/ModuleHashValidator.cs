using System.Security.Cryptography;

namespace Ginkgo.Api.Modules;

/// <summary>
/// 模块文件哈希校验结果
/// </summary>
public sealed class ModuleHashValidationResult
{
    public bool IsValid { get; set; } = true;
    /// <summary>
    /// 哈希校验不通过的文件列表（路径 → 错误描述）
    /// </summary>
    public List<string> Mismatches { get; set; } = new();
    /// <summary>
    /// 警告信息（如 module.json 中未声明 files 字段）
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// 模块包 SHA256 文件级哈希校验器
/// </summary>
public sealed class ModuleHashValidator
{
    /// <summary>
    /// 校验解压后的模块包中每个文件的 SHA256 哈希值是否与 module.json 中的声明一致
    /// </summary>
    /// <param name="manifest">解析后的模块清单</param>
    /// <param name="extractedPath">解压后的根目录</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>校验结果</returns>
    public async Task<ModuleHashValidationResult> ValidateAsync(
        ModuleManifest manifest, string extractedPath, CancellationToken ct = default)
    {
        var result = new ModuleHashValidationResult();

        // 如果 module.json 中未声明 files 字段，记录警告但不阻断（向后兼容）
        if (manifest.Files == null || manifest.Files.Length == 0)
        {
            result.Warnings.Add("module.json 中未声明 files 字段，跳过哈希校验。建议补充文件哈希以确保供应链安全。");
            return result;
        }

        foreach (var declaredFile in manifest.Files)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(declaredFile.Path))
            {
                result.Mismatches.Add("files 中存在空路径声明");
                result.IsValid = false;
                continue;
            }

            // 规范化路径分隔符
            var relativePath = declaredFile.Path.Replace('/', Path.DirectorySeparatorChar);

            // 路径安全检查：禁止 .. 等遍历攻击
            if (relativePath.Contains(".." + Path.DirectorySeparatorChar) ||
                relativePath.StartsWith(".." ) ||
                Path.IsPathRooted(relativePath))
            {
                result.Mismatches.Add($"文件路径包含非法遍历字符: {declaredFile.Path}");
                result.IsValid = false;
                continue;
            }

            var fullPath = Path.GetFullPath(Path.Combine(extractedPath, relativePath));

            // 确保路径在解压目录范围内
            if (!fullPath.StartsWith(Path.GetFullPath(extractedPath) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                result.Mismatches.Add($"文件路径逃逸出模块包范围: {declaredFile.Path}");
                result.IsValid = false;
                continue;
            }

            if (!File.Exists(fullPath))
            {
                result.Mismatches.Add($"声明的文件不存在: {declaredFile.Path}");
                result.IsValid = false;
                continue;
            }

            if (string.IsNullOrWhiteSpace(declaredFile.Sha256))
            {
                result.Warnings.Add($"文件 {declaredFile.Path} 未声明 sha256 值，跳过校验");
                continue;
            }

            // 计算实际文件的 SHA256
            var actualHash = await ComputeSha256Async(fullPath, ct);
            if (!string.Equals(actualHash, declaredFile.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                result.Mismatches.Add(
                    $"文件哈希不匹配: {declaredFile.Path}（期望: {declaredFile.Sha256[..Math.Min(16, declaredFile.Sha256.Length)]}...，实际: {actualHash[..16]}...）");
                result.IsValid = false;
            }
        }

        return result;
    }

    /// <summary>
    /// 计算文件的 SHA256 哈希值（小写十六进制）
    /// </summary>
    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct = default)
    {
        using var sha256 = SHA256.Create();
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        var hash = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
