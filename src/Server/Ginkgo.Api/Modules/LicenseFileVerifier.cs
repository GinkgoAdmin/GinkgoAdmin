using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ginkgo.Api.Modules;

/// <summary>
/// 用户站点端的 license.lic 文件结构（与商城 LicenseFilePayload / LicenseFile 字段一一镜像）。
/// 不能直接引用商城模块的 DTO（ALC 隔离），故本地维护一份镜像。
/// </summary>
public class ClientLicenseFile
{
    [JsonPropertyName("payload")] public ClientLicenseFilePayload Payload { get; set; } = new();
    [JsonPropertyName("signature")] public string Signature { get; set; } = string.Empty;
    [JsonPropertyName("algorithm")] public string Algorithm { get; set; } = string.Empty;
}

public class ClientLicenseFilePayload
{
    public int Schema { get; set; } = 1;
    public string LicenseKey { get; set; } = string.Empty;
    public string ModuleId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string EditionName { get; set; } = string.Empty;
    public long UserId { get; set; }
    public string OwnershipType { get; set; } = "perpetual";
    public DateTime? OwnershipExpireAt { get; set; }
    public DateTime? UpgradeExpireAt { get; set; }
    public string BindingPolicySnapshot { get; set; } = "{}";
    public int MaxActivations { get; set; }
    public string? ActivationId { get; set; }
    public string? Domain { get; set; }
    public string? MachineFingerprint { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime LicenseFileExpireAt { get; set; }
    public string PublicKeyFingerprint { get; set; } = string.Empty;
}

/// <summary>license 验签结果</summary>
public class LicenseFileValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public ClientLicenseFile? File { get; set; }
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// license.lic 验签器：使用商城公钥（PluginStore:LicensePublicKeyPem）验证 ECDSA P-256 签名。
/// 公钥可通过 GET /api/plugin-store/downloads/license-public-key 拉取后写入本站 appsettings 或 db.json。
/// </summary>
public sealed class LicenseFileVerifier : IDisposable
{
    private readonly ILogger<LicenseFileVerifier> _logger;
    private readonly ECDsa? _ecdsa;
    private readonly string? _publicKeyFingerprint;
    private readonly bool _strictMode;

    public LicenseFileVerifier(IConfiguration configuration, ILogger<LicenseFileVerifier> logger)
    {
        _logger = logger;
        var pem = configuration["PluginStore:LicensePublicKeyPem"]?.Trim();
        _strictMode = configuration.GetValue("PluginStore:LicenseStrictMode", false);

        if (string.IsNullOrWhiteSpace(pem))
        {
            _logger.LogWarning("未配置 PluginStore:LicensePublicKeyPem，license.lic 验签将被跳过（仅警告）。建议从商城 GET /api/plugin-store/downloads/license-public-key 获取公钥并写入配置。");
            return;
        }
        try
        {
            _ecdsa = ECDsa.Create();
            _ecdsa.ImportFromPem(pem);
            if (_ecdsa.KeySize != 256)
            {
                _logger.LogError("LicensePublicKeyPem KeySize={K} 非预期 256，验签禁用", _ecdsa.KeySize);
                _ecdsa.Dispose();
                _ecdsa = null;
                return;
            }
            _publicKeyFingerprint = ComputeFingerprint(pem);
            _logger.LogInformation("license.lic 验签器已加载（公钥指纹={Fp}）", _publicKeyFingerprint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析 LicensePublicKeyPem 失败，验签禁用");
            _ecdsa?.Dispose();
            _ecdsa = null;
        }
    }

    public bool IsConfigured => _ecdsa != null;
    public string? PublicKeyFingerprint => _publicKeyFingerprint;

    /// <summary>
    /// 验证 license.lic 字节并对比 moduleId / 域名 / 指纹 / 有效期。
    /// </summary>
    public LicenseFileValidationResult Verify(
        byte[] licenseBytes,
        string expectedModuleId,
        string? currentDomain,
        string? currentMachineFingerprint)
    {
        var result = new LicenseFileValidationResult();
        ClientLicenseFile? file;
        try
        {
            file = JsonSerializer.Deserialize<ClientLicenseFile>(licenseBytes,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"license.lic 解析失败: {ex.Message}";
            return result;
        }
        if (file == null || file.Payload == null)
        {
            result.ErrorMessage = "license.lic 内容为空";
            return result;
        }
        result.File = file;

        // 1. 签名验证
        if (_ecdsa == null)
        {
            // 未配置公钥：未严格模式下放行 + 警告；严格模式直接失败
            if (_strictMode)
            {
                result.ErrorMessage = "本站未配置 license 验签公钥（PluginStore:LicensePublicKeyPem），但 LicenseStrictMode=true，已拒绝安装。";
                return result;
            }
            result.Warnings.Add("⚠ 本站未配置 license 验签公钥，license 签名未被校验（建议尽快配置）");
        }
        else
        {
            try
            {
                var canonical = SerializeCanonical(file.Payload);
                var sigBytes = Convert.FromBase64String(file.Signature);
                var ok = _ecdsa.VerifyData(Encoding.UTF8.GetBytes(canonical), sigBytes, HashAlgorithmName.SHA256);
                if (!ok)
                {
                    result.ErrorMessage = "license.lic 签名校验失败（可能是伪造或公钥不匹配）";
                    return result;
                }
                // 公钥指纹对比（不强制，但要警告）
                if (!string.IsNullOrEmpty(_publicKeyFingerprint)
                    && !string.IsNullOrEmpty(file.Payload.PublicKeyFingerprint)
                    && !string.Equals(_publicKeyFingerprint, file.Payload.PublicKeyFingerprint, StringComparison.OrdinalIgnoreCase))
                {
                    result.Warnings.Add($"⚠ license 中公钥指纹({file.Payload.PublicKeyFingerprint}) 与本站配置的公钥指纹({_publicKeyFingerprint}) 不一致");
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"license.lic 验签异常: {ex.Message}";
                return result;
            }
        }

        // 2. ModuleId 匹配
        if (!string.Equals(file.Payload.ModuleId, expectedModuleId, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(file.Payload.ModuleId))
        {
            result.ErrorMessage = $"license 与插件包不匹配：license.ModuleId={file.Payload.ModuleId}, package.ModuleId={expectedModuleId}";
            return result;
        }
        if (string.IsNullOrEmpty(file.Payload.ModuleId))
            result.Warnings.Add("license 未声明 ModuleId（可能为系统类商品）");

        // 3. 文件有效期（防止旧 license 长期离线滥用）
        var now = DateTime.Now;
        if (file.Payload.LicenseFileExpireAt > DateTime.MinValue && file.Payload.LicenseFileExpireAt < now)
        {
            result.ErrorMessage = $"license.lic 已过期（{file.Payload.LicenseFileExpireAt:yyyy-MM-dd}），请重新激活";
            return result;
        }

        // 4. 所有权过期
        if (file.Payload.OwnershipExpireAt.HasValue && file.Payload.OwnershipExpireAt.Value < now)
        {
            result.ErrorMessage = $"授权已到期（{file.Payload.OwnershipExpireAt.Value:yyyy-MM-dd}），不能继续安装";
            return result;
        }

        // 5. 绑定策略校验
        var policy = ParseBindingPolicy(file.Payload.BindingPolicySnapshot);
        if (policy.Domain && !string.IsNullOrWhiteSpace(file.Payload.Domain))
        {
            if (!string.IsNullOrWhiteSpace(currentDomain)
                && !string.Equals(currentDomain.Trim().ToLowerInvariant(), file.Payload.Domain.Trim().ToLowerInvariant(), StringComparison.Ordinal))
            {
                result.ErrorMessage = $"绑定域名不匹配：license.Domain={file.Payload.Domain}, current={currentDomain}";
                return result;
            }
        }
        if (policy.Machine && !string.IsNullOrWhiteSpace(file.Payload.MachineFingerprint))
        {
            if (!string.IsNullOrWhiteSpace(currentMachineFingerprint)
                && !string.Equals(currentMachineFingerprint, file.Payload.MachineFingerprint, StringComparison.Ordinal))
            {
                result.ErrorMessage = $"绑定机器不匹配：license.MachineFingerprint={file.Payload.MachineFingerprint}, current={currentMachineFingerprint}";
                return result;
            }
        }

        result.IsValid = true;
        return result;
    }

    /// <summary>规范化 JSON：必须与商城 LicenseSigner.SerializeCanonical 字节级一致</summary>
    private static string SerializeCanonical(ClientLicenseFilePayload payload)
    {
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }

    private static string ComputeFingerprint(string publicKeyPem)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(publicKeyPem));
        return Convert.ToHexString(hash, 0, 16).ToLowerInvariant();
    }

    private static (bool Account, bool Tenant, bool Domain, bool Machine) ParseBindingPolicy(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return (true, false, false, false);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return (
                Account: root.TryGetProperty("account", out var a) && a.GetBoolean(),
                Tenant: root.TryGetProperty("tenant", out var t) && t.GetBoolean(),
                Domain: root.TryGetProperty("domain", out var d) && d.GetBoolean(),
                Machine: root.TryGetProperty("machine", out var m) && m.GetBoolean()
            );
        }
        catch
        {
            return (true, false, false, false);
        }
    }

    public void Dispose() => _ecdsa?.Dispose();
}

/// <summary>
/// 机器指纹生成：基于本机 MAC 地址 + 主机名 + ContentRoot 路径计算 SHA256，截取前 16 字节 hex。
/// 用于 license 绑定策略中的 Machine 维度匹配。
/// </summary>
public static class MachineFingerprintProvider
{
    private static string? _cached;
    private static readonly object _lock = new();

    public static string Get(string contentRoot)
    {
        if (_cached != null) return _cached;
        lock (_lock)
        {
            if (_cached != null) return _cached;
            var sb = new StringBuilder();
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up
                                && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .OrderBy(n => n.Id)
                    .Take(3);
                foreach (var nic in nics)
                {
                    var mac = nic.GetPhysicalAddress()?.ToString();
                    if (!string.IsNullOrWhiteSpace(mac)) sb.Append(mac).Append('|');
                }
            }
            catch { /* 网卡读取失败时仅依赖主机名 + 路径 */ }

            try { sb.Append(Environment.MachineName).Append('|'); } catch { }
            sb.Append(contentRoot ?? string.Empty);

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
            _cached = Convert.ToHexString(hash, 0, 16).ToLowerInvariant();
            return _cached;
        }
    }
}
