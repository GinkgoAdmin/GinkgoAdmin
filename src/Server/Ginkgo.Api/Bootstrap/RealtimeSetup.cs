using Ginkgo.Api.Services;
using Ginkgo.Domain.Logs;
using Serilog;

namespace Ginkgo.Api.Bootstrap;

/// <summary>
/// SignalR / 消息队列 / 后台服务注册（从 Program.cs 提取）。
/// </summary>
public static class RealtimeSetup
{
    /// <summary>
    /// 注册 SignalR + 消息队列后端（InMemory / RabbitMQ 二选一）。
    /// </summary>
    public static IServiceCollection AddRealtimeServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSignalR();
        services.AddSingleton<Ginkgo.Realtime.IRealtimeNotifier, Ginkgo.Realtime.RealtimeNotifier>();

        // 队列后端可选：InMemory / RabbitMQ（通过配置 Queue:Backend 一键切换）
        var queueBackend = configuration["Queue:Backend"] ?? "InMemory";
        if (string.Equals(queueBackend, "Rabbit", StringComparison.OrdinalIgnoreCase))
        {
            var host = configuration["Queue:Rabbit:Host"] ?? "localhost";
            var port = int.TryParse(configuration["Queue:Rabbit:Port"], out var p) ? p : 5672;
            var user = configuration["Queue:Rabbit:User"] ?? "guest";
            var password = configuration["Queue:Rabbit:Password"] ?? "guest";
            var vhost = configuration["Queue:Rabbit:VHost"] ?? "/";
            services.AddSingleton<Ginkgo.Realtime.RabbitQueue>(_ => new Ginkgo.Realtime.RabbitQueue(host, port, user, password, vhost));
            services.AddSingleton<Ginkgo.Realtime.IQueuePublisher>(sp => sp.GetRequiredService<Ginkgo.Realtime.RabbitQueue>());
            services.AddSingleton<Ginkgo.Realtime.IQueueSubscriber>(sp => sp.GetRequiredService<Ginkgo.Realtime.RabbitQueue>());
        }
        else
        {
            services.AddSingleton<Ginkgo.Realtime.InMemoryQueue>();
            services.AddSingleton<Ginkgo.Realtime.IQueuePublisher>(sp => sp.GetRequiredService<Ginkgo.Realtime.InMemoryQueue>());
            services.AddSingleton<Ginkgo.Realtime.IQueueSubscriber>(sp => sp.GetRequiredService<Ginkgo.Realtime.InMemoryQueue>());
        }

        // 后台服务
        services.AddSingleton<IOperationLogQueue, OperationLogQueue>();
        services.AddHostedService(sp => (OperationLogQueue)sp.GetRequiredService<IOperationLogQueue>());
        services.AddHostedService<Ginkgo.Realtime.RealtimeNotifyConsumer>();

        return services;
    }
}
