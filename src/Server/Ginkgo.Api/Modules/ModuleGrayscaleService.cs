using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Api.Modules;

/// <summary>
/// 灰度发布策略
/// </summary>
public sealed class GrayscalePolicy
{
    /// <summary>
    /// 通道：stable / beta / dev
    /// </summary>
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "stable";

    /// <summary>
    /// 灰度目标租户 ID 列表（为空或 null 表示全量发布）
    /// </summary>
    [JsonPropertyName("targetTenantIds")]
    public List<string>? TargetTenantIds { get; set; }

    /// <summary>
    /// 灰度开始时间
    /// </summary>
    [JsonPropertyName("startTime")]
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 灰度结束时间（到期自动全量或回退，取决于 autoPromote）
    /// </summary>
    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 灰度到期后是否自动全量发布（false 则自动回退为禁用）
    /// </summary>
    [JsonPropertyName("autoPromote")]
    public bool AutoPromote { get; set; } = false;

    /// <summary>
    /// 创建人
    /// </summary>
    [JsonPropertyName("createdBy")]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 灰度发布管理服务。
/// 灰度策略存储在 modules_repo/grayscale_policies.json 文件中，
/// 不修改主框架 Domain 层的实体定义。
/// </summary>
public sealed class ModuleGrayscaleService
{
    private readonly string _policyFilePath;
    private readonly ILogger<ModuleGrayscaleService> _logger;
    private readonly object _lock = new();
    private Dictionary<string, GrayscalePolicy> _policies = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public ModuleGrayscaleService(ILogger<ModuleGrayscaleService> logger)
    {
        _logger = logger;
        _policyFilePath = Path.Combine(AppContext.BaseDirectory, "modules_repo", "grayscale_policies.json");
        Load();
    }

    /// <summary>
    /// 从文件加载策略
    /// </summary>
    private void Load()
    {
        try
        {
            if (File.Exists(_policyFilePath))
            {
                var json = File.ReadAllText(_policyFilePath);
                _policies = JsonSerializer.Deserialize<Dictionary<string, GrayscalePolicy>>(json, JsonOptions)
                            ?? new(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "加载灰度策略文件失败: {Path}", _policyFilePath);
        }
    }

    /// <summary>
    /// 持久化策略到文件
    /// </summary>
    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_policyFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_policies, JsonOptions);
            File.WriteAllText(_policyFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存灰度策略文件失败: {Path}", _policyFilePath);
        }
    }

    /// <summary>
    /// 设置模块的灰度策略
    /// </summary>
    public void SetPolicy(string moduleId, GrayscalePolicy policy)
    {
        lock (_lock)
        {
            _policies[moduleId] = policy;
            Save();
        }
        _logger.LogInformation("模块 {ModuleId} 灰度策略已更新: 通道={Channel}, 目标租户数={Count}",
            moduleId, policy.Channel, policy.TargetTenantIds?.Count ?? 0);
    }

    /// <summary>
    /// 移除模块的灰度策略（全量发布）
    /// </summary>
    public void RemovePolicy(string moduleId)
    {
        lock (_lock)
        {
            _policies.Remove(moduleId);
            Save();
        }
        _logger.LogInformation("模块 {ModuleId} 灰度策略已移除（全量发布）", moduleId);
    }

    /// <summary>
    /// 获取模块的灰度策略
    /// </summary>
    public GrayscalePolicy? GetPolicy(string moduleId)
    {
        lock (_lock)
        {
            return _policies.TryGetValue(moduleId, out var p) ? p : null;
        }
    }

    /// <summary>
    /// 获取所有灰度策略
    /// </summary>
    public Dictionary<string, GrayscalePolicy> GetAllPolicies()
    {
        lock (_lock)
        {
            return new Dictionary<string, GrayscalePolicy>(_policies, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 判断指定租户是否应该加载指定模块（灰度判定）
    /// </summary>
    /// <param name="moduleId">模块 ID</param>
    /// <param name="tenantId">当前租户 ID（null 表示无租户上下文，按全量处理）</param>
    /// <returns>true 表示该租户应该加载此模块</returns>
    public bool ShouldLoad(string moduleId, string? tenantId)
    {
        lock (_lock)
        {
            if (!_policies.TryGetValue(moduleId, out var policy))
                return true; // 无灰度策略 = 全量加载

            var now = DateTime.Now;

            // 检查灰度时间窗口
            if (policy.StartTime.HasValue && now < policy.StartTime.Value)
                return false; // 未到灰度开始时间

            if (policy.EndTime.HasValue && now > policy.EndTime.Value)
            {
                // 灰度已过期
                if (policy.AutoPromote)
                {
                    // 自动全量：移除策略
                    _policies.Remove(moduleId);
                    Save();
                    _logger.LogInformation("模块 {ModuleId} 灰度到期，自动全量发布", moduleId);
                    return true;
                }
                else
                {
                    // 自动回退：不加载
                    return false;
                }
            }

            // 无目标租户限制 = 全量
            if (policy.TargetTenantIds == null || policy.TargetTenantIds.Count == 0)
                return true;

            // 无租户上下文时不加载灰度模块
            if (string.IsNullOrEmpty(tenantId))
                return false;

            return policy.TargetTenantIds.Contains(tenantId, StringComparer.OrdinalIgnoreCase);
        }
    }
}
