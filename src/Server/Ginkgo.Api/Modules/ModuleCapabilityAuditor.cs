using System.Text.Json;
using Ginkgo.Domain;
using Ginkgo.Domain.Modules;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Api.Modules;

/// <summary>
/// 模块能力声明审计器。
/// 模块在 module.json 的 capabilities 字段中声明自己需要的能力，
/// 安装时由管理员确认审批，运行时记录审计日志。
/// </summary>
public sealed class ModuleCapabilityAuditor
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ModuleCapabilityAuditor> _logger;

    /// <summary>
    /// 系统定义的所有合法能力标识及其中文描述
    /// </summary>
    public static readonly Dictionary<string, string> KnownCapabilities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["database:read"] = "数据库读取",
        ["database:write"] = "数据库写入",
        ["filesystem:read"] = "文件系统读取",
        ["filesystem:write"] = "文件系统写入",
        ["network:outbound"] = "外部网络请求",
        ["scheduler"] = "定时任务",
        ["ai:tools"] = "AI 工具注册",
        ["notification"] = "系统通知",
        ["user:context"] = "用户上下文访问",
        ["menus"] = "菜单注册",
        ["settings"] = "配置管理",
        ["realtime"] = "实时通信（SignalR）",
        ["storage"] = "文件存储（OSS/本地）"
    };

    public ModuleCapabilityAuditor(IServiceProvider services, ILogger<ModuleCapabilityAuditor> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// 解析模块声明的能力列表，返回结构化的能力信息（供前端安装确认弹窗展示）
    /// </summary>
    public List<CapabilityInfo> ParseCapabilities(ModuleManifest manifest)
    {
        var result = new List<CapabilityInfo>();
        if (manifest.Capabilities == null || manifest.Capabilities.Length == 0)
            return result;

        foreach (var cap in manifest.Capabilities)
        {
            var known = KnownCapabilities.TryGetValue(cap, out var desc);
            result.Add(new CapabilityInfo
            {
                Id = cap,
                Name = desc ?? cap,
                IsKnown = known,
                RiskLevel = ClassifyRisk(cap)
            });
        }

        return result;
    }

    /// <summary>
    /// 记录模块安装时的能力审批日志
    /// </summary>
    public void AuditInstall(string moduleId, string? publisher, string[]? capabilities, string? operatorId)
    {
        try
        {
            var repo = _services.GetService(typeof(IRepository<ModuleOpLogEntity>)) as IRepository<ModuleOpLogEntity>;
            if (repo == null) return;

            var log = new ModuleOpLogEntity
            {
                Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId(),
                ModuleId = moduleId,
                Action = "Security.CapabilityApproved",
                Level = "INFO",
                CreatedAtUtc = DateTime.Now,
                Message = $"管理员审批了模块 {moduleId} 的能力声明",
                DetailsJson = JsonSerializer.Serialize(new
                {
                    publisher,
                    capabilities = capabilities ?? Array.Empty<string>(),
                    operatorId,
                    approvedAt = DateTime.Now
                })
            };
            _ = repo.AddAsync(log);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "记录能力审批日志失败: {ModuleId}", moduleId);
        }
    }

    /// <summary>
    /// 记录运行时能力使用审计（当前阶段为异步日志，不做拦截）
    /// </summary>
    public void AuditUsage(string moduleId, string capability, string? context = null)
    {
        _logger.LogDebug("模块 {ModuleId} 使用能力 {Capability}: {Context}", moduleId, capability, context);
    }

    /// <summary>
    /// 对能力进行风险分级
    /// </summary>
    private static string ClassifyRisk(string capability)
    {
        return capability.ToLowerInvariant() switch
        {
            "database:write" => "high",
            "filesystem:write" => "high",
            "network:outbound" => "medium",
            "ai:tools" => "medium",
            "scheduler" => "medium",
            "database:read" => "low",
            "filesystem:read" => "low",
            "user:context" => "low",
            "menus" => "low",
            "settings" => "low",
            "notification" => "low",
            "realtime" => "low",
            "storage" => "medium",
            _ => "unknown"
        };
    }
}

/// <summary>
/// 能力信息（供前端展示）
/// </summary>
public sealed class CapabilityInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsKnown { get; set; }
    /// <summary>
    /// 风险等级：low / medium / high / unknown
    /// </summary>
    public string RiskLevel { get; set; } = "unknown";
}
