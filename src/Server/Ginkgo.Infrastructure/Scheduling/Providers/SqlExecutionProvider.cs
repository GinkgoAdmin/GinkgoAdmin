// 文件功能说明：
// 内置执行提供器：执行 SQL 脚本。
// 运维人员编写 SQL 语句，定时执行数据库维护操作。
// 包含安全校验：禁止 DROP、TRUNCATE、ALTER、CREATE、GRANT、REVOKE 等危险语句。

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ginkgo.Plugin.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Ginkgo.Infrastructure.Scheduling.Providers;

/// <summary>
/// SQL 脚本执行提供器 — 定时执行数据库维护 SQL。
/// </summary>
public sealed class SqlExecutionProvider : ITaskExecutionProvider
{
    public string SourceKey => "Sql";
    public string DisplayName => "SQL 脚本";
    public string? Icon => "bi-database";
    public string? Description => "定时执行数据库维护 SQL（支持 SELECT/INSERT/UPDATE/DELETE/CALL）";
    public int Order => 30;
    public bool SupportsTest => true;

    // 禁止的 SQL 关键字（不区分大小写）
    private static readonly string[] ForbiddenKeywords = { "DROP", "TRUNCATE", "ALTER", "CREATE", "GRANT", "REVOKE" };

    public ExecutionFormDefinition GetFormDefinition()
    {
        return new ExecutionFormDefinition
        {
            Fields = new[]
            {
                new ExecutionFormField
                {
                    Name = "sqlScript",
                    Label = "SQL 脚本",
                    Type = "code-editor",
                    Required = true,
                    Rows = 8,
                    Placeholder = "DELETE FROM ginkgo_Sys_OperationLog WHERE CreatedAt < DATE_SUB(NOW(), INTERVAL 90 DAY)",
                    Description = "支持 SELECT/INSERT/UPDATE/DELETE/CALL，禁止 DROP/TRUNCATE/ALTER/CREATE/GRANT/REVOKE"
                },
                new ExecutionFormField
                {
                    Name = "timeoutSeconds",
                    Label = "超时时间（秒）",
                    Type = "number",
                    DefaultValue = 60,
                    MinValue = 5,
                    MaxValue = 600
                }
            }
        };
    }

    public Task<ExecutionValidationResult> ValidateAsync(string configJson, IServiceProvider services)
    {
        try
        {
            var config = JsonSerializer.Deserialize<SqlConfig>(configJson, _jsonOpts);
            if (config == null || string.IsNullOrWhiteSpace(config.SqlScript))
                return Task.FromResult(ExecutionValidationResult.Fail("SQL 脚本不能为空"));

            var safetyResult = CheckSqlSafety(config.SqlScript);
            if (!safetyResult.IsValid)
                return Task.FromResult(safetyResult);

            return Task.FromResult(ExecutionValidationResult.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(ExecutionValidationResult.Fail($"配置格式错误: {ex.Message}"));
        }
    }

    public async Task<ActionExecutionResult> ExecuteAsync(string configJson, ActionContext context)
    {
        var config = JsonSerializer.Deserialize<SqlConfig>(configJson, _jsonOpts)
            ?? throw new InvalidOperationException("ConfigJson 反序列化失败");

        var safetyResult = CheckSqlSafety(config.SqlScript!);
        if (!safetyResult.IsValid)
            return ActionExecutionResult.Fail($"SQL 安全校验不通过: {safetyResult.ErrorMessage}");

        var timeout = Math.Clamp(config.TimeoutSeconds ?? 60, 5, 600);
        var db = context.Services.GetRequiredService<ISqlSugarClient>();

        var sw = Stopwatch.StartNew();
        var affectedRows = await db.Ado.ExecuteCommandAsync(config.SqlScript!, context.CancellationToken);
        sw.Stop();

        context.Logger.LogInformation("SQL 任务执行完成，影响 {Rows} 行，耗时 {Elapsed}ms", affectedRows, sw.ElapsedMilliseconds);

        return new ActionExecutionResult
        {
            Success = true,
            Message = $"SQL 执行成功，影响 {affectedRows} 行 ({sw.ElapsedMilliseconds}ms)",
            Data = new { affectedRows, elapsed = sw.ElapsedMilliseconds }
        };
    }

    public async Task<ActionExecutionResult> TestAsync(string configJson, ActionContext context)
    {
        var config = JsonSerializer.Deserialize<SqlConfig>(configJson, _jsonOpts)
            ?? throw new InvalidOperationException("ConfigJson 反序列化失败");

        var safetyResult = CheckSqlSafety(config.SqlScript!);
        if (!safetyResult.IsValid)
            return ActionExecutionResult.Fail($"SQL 安全校验不通过: {safetyResult.ErrorMessage}");

        // 测试模式：用 SELECT 1 校验连接，并展示 SQL 预览
        var db = context.Services.GetRequiredService<ISqlSugarClient>();
        await db.Ado.ExecuteCommandAsync("SELECT 1", context.CancellationToken);

        return ActionExecutionResult.Ok(
            $"连接测试成功。SQL 预览：{(config.SqlScript!.Length > 200 ? config.SqlScript[..200] + "..." : config.SqlScript)}");
    }

    /// <summary>
    /// 检查 SQL 安全性。
    /// </summary>
    private static ExecutionValidationResult CheckSqlSafety(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return ExecutionValidationResult.Fail("SQL 脚本不能为空");

        // 去除注释后检查
        var cleaned = Regex.Replace(sql, @"--[^\n]*", " ");
        cleaned = Regex.Replace(cleaned, @"/\*[\s\S]*?\*/", " ");

        foreach (var keyword in ForbiddenKeywords)
        {
            // 检查是否包含禁止关键字（前后必须是非字母字符或行首/行尾）
            var pattern = $@"(?<![A-Za-z_]){Regex.Escape(keyword)}(?![A-Za-z_])";
            if (Regex.IsMatch(cleaned, pattern, RegexOptions.IgnoreCase))
            {
                return ExecutionValidationResult.Fail($"SQL 中包含禁止的关键字: {keyword}。如确需使用请联系开发人员创建内置能力。");
            }
        }

        return ExecutionValidationResult.Ok();
    }

    private sealed class SqlConfig
    {
        public string? SqlScript { get; set; }
        public int? TimeoutSeconds { get; set; }
    }

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
