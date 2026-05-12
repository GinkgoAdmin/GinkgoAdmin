// 文件功能说明：
// 慢查询事件后台消费者。从 SlowQueryReporter 内部 Channel 读取事件，
// 在独立 Scope 中调用 IOpLogRepository.AppendAsync 落库，避免阻塞 SQL 执行链路。
//
// 设计要点：
// - 仅当 Database.Features.SlowQuery.Enabled=true && WriteToOpLog=true 时由 DI 注册（参见 ServiceCollectionExtensions）。
// - 单 Reader 模型；ReadAllAsync 在通道关闭后自然退出，与 BackgroundService 的 stoppingToken 协同停机。
// - 写库异常仅记录日志、不再次抛出，避免后台线程崩溃。

using Ginkgo.Domain.Logs;
using Ginkgo.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Infrastructure.Persistence.Features;

/// <summary>
/// 慢查询事件后台消费者：把 SlowQueryReporter 中的事件写入 OpLog。
/// </summary>
public sealed class SlowQueryHostedService : BackgroundService
{
    private readonly SlowQueryReporter _reporter;
    private readonly IServiceProvider _services;
    private readonly ILogger<SlowQueryHostedService> _logger;

    public SlowQueryHostedService(
        SlowQueryReporter reporter,
        IServiceProvider services,
        ILogger<SlowQueryHostedService> logger)
    {
        _reporter = reporter;
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[SlowQuery] 后台消费者启动，开始监听慢查询事件并异步落入 OpLog");

        try
        {
            await foreach (var ev in _reporter.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var repo = scope.ServiceProvider.GetService<IOpLogRepository>();
                    if (repo == null)
                    {
                        // 启动初期或数据库未就绪时仓储未注册，跳过；不阻塞通道。
                        continue;
                    }

                    var entity = OpLog.Create(action: "SLOW_SQL", resource: "SqlSugar", createdBy: ev.UserId);
                    entity.Result = "SLOW";
                    entity.ElapsedMs = (int)Math.Min(ev.ElapsedMs, int.MaxValue);
                    entity.At = ev.At;
                    entity.CreatedAt = ev.At;
                    entity.ModuleCN = "数据库";
                    entity.FeatureCN = "慢查询";
                    entity.ReviewCN = "数据库-慢查询-记录";
                    entity.DataJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Sql = Truncate(ev.Sql, 4000),
                        ev.ElapsedMs,
                        ev.ThresholdMs
                    });

                    await repo.AppendAsync(entity, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // 应用关停：直接退出循环。
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[SlowQuery] 写入 OpLog 失败：{Message}", ex.Message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停机：忽略。
        }

        var dropped = _reporter.DroppedCount;
        if (dropped > 0)
        {
            _logger.LogInformation("[SlowQuery] 后台消费者停止；累计丢弃事件 {Dropped} 条（缓冲满或处理慢）", dropped);
        }
        else
        {
            _logger.LogInformation("[SlowQuery] 后台消费者停止");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _reporter.Complete();
        return base.StopAsync(cancellationToken);
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? (s ?? string.Empty) : s.Substring(0, max);
}
