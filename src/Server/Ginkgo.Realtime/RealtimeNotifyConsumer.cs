using System.Text.Json;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Ginkgo.Domain;
using Ginkgo.Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace Ginkgo.Realtime;

public sealed class RealtimeNotifyConsumer : BackgroundService
{
    private readonly IQueueSubscriber _sub;
    private readonly IHubContext<NotifyHub> _hub;
    private readonly IServiceScopeFactory _scopeFactory;
    public RealtimeNotifyConsumer(IQueueSubscriber sub, IHubContext<NotifyHub> hub, IServiceScopeFactory scopeFactory)
    {
        _sub = sub; _hub = hub; _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _sub.SubscribeAsync("notify.dispatch", async body =>
        {
            try
            {
                var obj = JsonSerializer.Deserialize<DispatchMessage>(body.Span);
                if (obj == null) return;
                if (obj.userId.HasValue)
                {
                    // 将 notifyId 转为字符串，避免 JavaScript 大整数精度丢失
                    await _hub.Clients.Group($"user:{obj.userId.Value}").SendAsync("Notify.Message", new { notifyId = obj.notifyId.ToString() }, stoppingToken);
                    // 更新投递状态（仅旧通知系统有 NotifyAudience 记录，新消息系统无需更新）
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var audRepo = scope.ServiceProvider.GetRequiredService<IRepository<NotifyAudience>>();
                        var aud = await audRepo.Query().FirstAsync(x => x.NotifyId == obj.notifyId && x.UserId == obj.userId.Value);
                        if (aud != null)
                        {
                            aud.DeliveryStatus = 1; // 已推送
                            aud.DeliveredAt = DateTime.Now;
                            await audRepo.UpdateAsync(aud, stoppingToken);
                        }
                    }
                    catch { /* 新消息系统无 NotifyAudience 记录，忽略 */ }
                }
                else
                {
                    await _hub.Clients.All.SendAsync("Notify.Message", new { notifyId = obj.notifyId.ToString() }, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RealtimeNotify] Consumer error: {ex.Message}");
            }
        }, stoppingToken);
    }

    private sealed class DispatchMessage
    {
        public long notifyId { get; set; }
        public long? userId { get; set; }
    }
}
