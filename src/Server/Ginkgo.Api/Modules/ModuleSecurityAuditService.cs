using System.Text.Json;
using System.Text.Json.Nodes;
using Ginkgo.Domain;
using Ginkgo.Domain.Modules;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Api.Modules;

/// <summary>
/// 模块安全审计服务：集中记录所有模块操作的审计日志，
/// 并在发生安全事件（签名失败、哈希不匹配、路径遍历）时触发告警。
/// </summary>
public sealed class ModuleSecurityAuditService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ModuleSecurityAuditService> _logger;

    public ModuleSecurityAuditService(IServiceProvider services, ILogger<ModuleSecurityAuditService> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// P1-8：审计日志中需要脱敏的字段名（大小写无关）。命中后值会被替换为 "***REDACTED***"，
    /// 避免 token / password / api_key / signature / connection_string 等敏感数据被持久化到 ModuleOpLog。
    /// </summary>
    private static readonly HashSet<string> _redactKeyFragments = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwd", "pwd",
        "secret",
        "token",
        "apikey", "api_key",
        "authorization", "auth",
        "credential",
        "privatekey", "private_key",
        "signature",
        "session",
        "cookie",
        "connectionstring", "connection_string", "connstr",
        "accesskey", "access_key",
        "refreshtoken", "refresh_token"
    };

    private const string RedactedPlaceholder = "***REDACTED***";

    /// <summary>
    /// 将任意对象先序列化为 JsonNode，再递归脱敏后输出 JSON 字符串。
    /// 不修改原对象。任何序列化失败都吞掉，回退到 "(详情序列化失败)" 占位。
    /// </summary>
    private static string? SerializeWithRedaction(object? details)
    {
        if (details == null) return null;
        try
        {
            var node = JsonSerializer.SerializeToNode(details);
            RedactNode(node);
            return node?.ToJsonString();
        }
        catch
        {
            return "{\"_error\":\"(详情序列化失败，已忽略以避免泄漏敏感字段)\"}";
        }
    }

    private static void RedactNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                // 收集需要替换的 key，避免在迭代时修改集合
                foreach (var key in obj.Select(kv => kv.Key).ToArray())
                {
                    if (ShouldRedact(key))
                    {
                        obj[key] = RedactedPlaceholder;
                    }
                    else
                    {
                        RedactNode(obj[key]);
                    }
                }
                break;
            case JsonArray arr:
                foreach (var child in arr)
                    RedactNode(child);
                break;
            // JsonValue / null 不处理
        }
    }

    private static bool ShouldRedact(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        foreach (var frag in _redactKeyFragments)
        {
            if (key.IndexOf(frag, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 记录模块操作审计日志（详情字段已做敏感字段脱敏，P1-8）。
    /// </summary>
    public void AuditOperation(string moduleId, string action, string? operatorId = null, string? message = null, object? details = null)
    {
        try
        {
            using var scope = _services.CreateScope();
            var repo = scope.ServiceProvider.GetService<IRepository<ModuleOpLogEntity>>();
            if (repo == null) return;

            var log = new ModuleOpLogEntity
            {
                Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId(),
                ModuleId = moduleId,
                Action = action,
                Level = "INFO",
                CreatedAtUtc = DateTime.Now,
                Message = message ?? $"模块 {moduleId} 执行操作: {action}",
                DetailsJson = SerializeWithRedaction(details)
            };
            _ = repo.AddAsync(log);
            _logger.LogInformation("[Audit] {ModuleId} {Action}: {Message}", moduleId, action, message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "记录审计日志失败: {ModuleId} {Action}", moduleId, action);
        }
    }

    /// <summary>
    /// 记录安全告警事件（签名失败、哈希不匹配、路径遍历尝试等，详情字段已做敏感字段脱敏，P1-8）。
    /// </summary>
    public void AuditSecurityEvent(string moduleId, string eventType, string message, object? details = null)
    {
        try
        {
            using var scope = _services.CreateScope();
            var repo = scope.ServiceProvider.GetService<IRepository<ModuleOpLogEntity>>();
            if (repo == null) return;

            var log = new ModuleOpLogEntity
            {
                Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId(),
                ModuleId = moduleId,
                Action = $"Security.{eventType}",
                Level = "ERROR",
                CreatedAtUtc = DateTime.Now,
                Message = $"[安全告警] {message}",
                DetailsJson = SerializeWithRedaction(details)
            };
            _ = repo.AddAsync(log);
            _logger.LogWarning("[SecurityAlert] {ModuleId} {EventType}: {Message}", moduleId, eventType, message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "记录安全事件失败: {ModuleId} {EventType}", moduleId, eventType);
        }
    }

    /// <summary>
    /// 处理上传验证结果，记录安全相关事件
    /// </summary>
    public void AuditUploadValidation(string moduleId, ModuleUploadValidationResult validation)
    {
        // 记录上传操作
        AuditOperation(moduleId, "Upload", message: $"模块包上传验证: {(validation.IsValid ? "通过" : "失败")}");

        // 哈希校验失败
        if (validation.HashValidation != null && !validation.HashValidation.IsValid)
        {
            AuditSecurityEvent(moduleId, "HashMismatch",
                $"文件哈希校验失败: {validation.HashValidation.Mismatches?.Count ?? 0} 个文件不匹配",
                new { mismatchedFiles = validation.HashValidation.Mismatches });
        }

        // 签名校验失败
        if (validation.SignatureValidation != null && !validation.SignatureValidation.IsValid)
        {
            AuditSecurityEvent(moduleId, "SignatureFailure",
                $"签名验证失败: {validation.SignatureValidation.ErrorMessage}",
                new { reason = validation.SignatureValidation.ErrorMessage });
        }

        // 安全警告
        if (validation.SecurityWarnings?.Count > 0)
        {
            AuditOperation(moduleId, "Security.Warnings",
                message: $"安全警告: {string.Join("; ", validation.SecurityWarnings)}",
                details: new { warnings = validation.SecurityWarnings });
        }
    }
}
