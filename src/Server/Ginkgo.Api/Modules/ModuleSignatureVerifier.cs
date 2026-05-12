using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Api.Modules;

/// <summary>
/// 模块签名校验结果
/// </summary>
public sealed class ModuleSignatureValidationResult
{
    public bool IsValid { get; set; } = true;
    /// <summary>
    /// 签名验证通过的发布者标识（匹配到的公钥名称）
    /// </summary>
    public string? MatchedPublisher { get; set; }
    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
    /// <summary>
    /// 警告信息（如未配置公钥、包未签名等）
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// 模块安全配置。
/// P1-2：<see cref="RequireSignature"/> / <see cref="RequireFileHashes"/> / <see cref="RequireDownloadHashHeader"/>
/// 改为 nullable，由 <see cref="ModuleSignatureVerifier"/> 在构造时根据 <see cref="IHostEnvironment"/> 决定默认值：
/// 生产环境默认 <c>true</c>，开发环境默认 <c>false</c>，appsettings.json 显式给值则以配置为准。
/// </summary>
public sealed class ModuleSecurityOptions
{
    /// <summary>
    /// 受信任的公钥列表（名称 → Base64 编码的 ECDSA P-256 公钥）
    /// </summary>
    public Dictionary<string, string> TrustedPublicKeys { get; set; } = new();
    /// <summary>
    /// 受信任的发布者白名单（为空时允许所有发布者）
    /// </summary>
    public string[] TrustedPublishers { get; set; } = Array.Empty<string>();
    /// <summary>
    /// 是否强制要求签名（生产默认 true，开发默认 false）。
    /// </summary>
    public bool? RequireSignature { get; set; }
    /// <summary>
    /// 是否强制要求文件哈希（生产默认 true，开发默认 false）。
    /// </summary>
    public bool? RequireFileHashes { get; set; }
    /// <summary>
    /// 客户端拉包时是否强制要求 X-Ginkgo-Package-SHA256 响应头（生产默认 true，开发默认 false）。
    /// 由 P1-6 控制 ModulesController.GetPackage 对哈希计算失败时的硬拒。
    /// </summary>
    public bool? RequireDownloadHashHeader { get; set; }
}

/// <summary>
/// 模块包 ECDSA P-256 签名验证器。
/// 签名对象为 module.json 文件内容的原始字节（module.json 中已包含各文件的 SHA256 哈希声明，
/// 形成"签名 → module.json → 文件哈希"的链式信任）。
/// </summary>
public sealed class ModuleSignatureVerifier
{
    private readonly ModuleSecurityOptions _options;
    private readonly ILogger<ModuleSignatureVerifier> _logger;

    public ModuleSignatureVerifier(IConfiguration configuration, IHostEnvironment env, ILogger<ModuleSignatureVerifier> logger)
    {
        _logger = logger;
        _options = new ModuleSecurityOptions();
        configuration.GetSection("ModuleSecurity").Bind(_options);

        // P1-2：未显式配置时，按运行环境给默认值。生产硬性要求签名/哈希；开发为兼容历史包默认放行但记录警告。
        var isProduction = env.IsProduction();
        _options.RequireSignature ??= isProduction;
        _options.RequireFileHashes ??= isProduction;
        _options.RequireDownloadHashHeader ??= isProduction;

        if (isProduction && _options.TrustedPublicKeys.Count == 0)
        {
            // 生产环境强制签名但未配置公钥 → 启动时给出明显警告（不抛错以避免阻塞已部署系统升级），
            // 真正的安装动作在 Verify() 里会被拒绝。
            _logger.LogWarning("[ModuleSecurity] 生产环境已开启 RequireSignature，但 ModuleSecurity:TrustedPublicKeys 为空，所有未签名模块将被拒绝安装。");
        }

        _logger.LogInformation(
            "[ModuleSecurity] 已加载安全配置：RequireSignature={Sig}, RequireFileHashes={Hash}, RequireDownloadHashHeader={Hdr}, TrustedPublicKeys={KeyCount}, TrustedPublishers={PubCount}",
            _options.RequireSignature, _options.RequireFileHashes, _options.RequireDownloadHashHeader,
            _options.TrustedPublicKeys.Count, _options.TrustedPublishers.Length);
    }

    /// <summary>
    /// 获取当前安全配置（供外部查询强制模式等）
    /// </summary>
    public ModuleSecurityOptions Options => _options;

    /// <summary>
    /// 验证模块包签名
    /// </summary>
    /// <param name="manifest">解析后的模块清单</param>
    /// <param name="moduleJsonRawBytes">module.json 文件的原始字节内容（签名验证对象）</param>
    /// <returns>签名验证结果</returns>
    public ModuleSignatureValidationResult Verify(ModuleManifest manifest, byte[] moduleJsonRawBytes)
    {
        var result = new ModuleSignatureValidationResult();

        // 如果未配置任何受信任公钥，跳过签名验证
        if (_options.TrustedPublicKeys.Count == 0)
        {
            if (_options.RequireSignature == true)
            {
                result.IsValid = false;
                result.ErrorMessage = "系统要求签名验证但未配置任何受信任公钥，请在 appsettings.json 的 ModuleSecurity:TrustedPublicKeys 中配置。";
                return result;
            }

            result.Warnings.Add("未配置受信任公钥，跳过签名验证。");
            return result;
        }

        // 如果模块包未携带签名
        if (string.IsNullOrWhiteSpace(manifest.Signature))
        {
            if (_options.RequireSignature == true)
            {
                result.IsValid = false;
                result.ErrorMessage = $"模块 {manifest.Id} 未携带数字签名，系统要求所有模块包必须签名。";
                return result;
            }

            result.Warnings.Add($"模块 {manifest.Id} 未携带数字签名，建议使用 gmod sign 工具签名。");
            return result;
        }

        // 解析签名值（Base64 编码）
        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromBase64String(manifest.Signature);
        }
        catch (FormatException)
        {
            result.IsValid = false;
            result.ErrorMessage = $"模块 {manifest.Id} 的签名格式无效（非法 Base64）。";
            return result;
        }

        // 计算 module.json 原始内容的 SHA256 摘要
        var contentHash = SHA256.HashData(moduleJsonRawBytes);

        // 遍历所有受信任公钥尝试验证
        foreach (var (keyName, keyBase64) in _options.TrustedPublicKeys)
        {
            try
            {
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(keyBase64), out _);

                if (ecdsa.VerifyHash(contentHash, signatureBytes))
                {
                    result.IsValid = true;
                    result.MatchedPublisher = keyName;
                    _logger.LogInformation("模块 {ModuleId} 签名验证通过，匹配公钥: {KeyName}", manifest.Id, keyName);
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "使用公钥 {KeyName} 验证模块 {ModuleId} 签名时出错", keyName, manifest.Id);
            }
        }

        // 所有公钥都无法验证
        result.IsValid = false;
        result.ErrorMessage = $"模块 {manifest.Id} 的签名无法通过任何受信任公钥验证，该包可能被篡改或使用了未授权的密钥签名。";
        _logger.LogWarning("模块 {ModuleId} 签名验证失败，尝试了 {KeyCount} 个受信任公钥均不匹配",
            manifest.Id, _options.TrustedPublicKeys.Count);

        return result;
    }

    /// <summary>
    /// 校验发布者是否在白名单中
    /// </summary>
    /// <param name="publisher">模块声明的发布者</param>
    /// <returns>(通过, 错误/警告消息)</returns>
    public (bool Ok, string? Message) ValidatePublisher(string? publisher)
    {
        // 白名单为空时允许所有发布者
        if (_options.TrustedPublishers.Length == 0)
            return (true, null);

        if (string.IsNullOrWhiteSpace(publisher))
            return (false, "模块未声明发布者（publisher），但系统已配置发布者白名单，拒绝安装。");

        if (_options.TrustedPublishers.Any(p => string.Equals(p, publisher, StringComparison.OrdinalIgnoreCase)))
            return (true, null);

        return (false, $"发布者 \"{publisher}\" 不在受信任发布者白名单中，拒绝安装。");
    }
}
