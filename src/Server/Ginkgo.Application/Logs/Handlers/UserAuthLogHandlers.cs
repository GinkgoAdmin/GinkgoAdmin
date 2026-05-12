using Ginkgo.Application.Logs;
using Ginkgo.Domain.Events;
using Ginkgo.Domain.Users.Events;

namespace Ginkgo.Application.Logs.Handlers;

/// <summary>
/// 认证相关领域事件处理：将用户登录/登出事件转换为操作日志。
/// </summary>
public sealed class UserLoggedInHandler : IDomainEventHandler<UserLoggedIn>
{
    private readonly ILogAppService _logs;
    public UserLoggedInHandler(ILogAppService logs) { _logs = logs; }

    public async Task HandleAsync(UserLoggedIn @event, CancellationToken ct = default)
    {
        var input = new AppendOpLogInput
        {
            Action = "POST",
            Resource = "/api/auth/login",
            ModuleCN = "认证",
            FeatureCN = "登录",
            ReviewCN = "认证-登录-成功",
            Result = "OK",
            CreatedAt = DateTime.Now,
            CreatedBy = @event.UserId,
            Ip = @event.Ip
        };
        await _logs.AppendAsync(input, ct);
    }
}

public sealed class UserLoggedOutHandler : IDomainEventHandler<UserLoggedOut>
{
    private readonly ILogAppService _logs;
    public UserLoggedOutHandler(ILogAppService logs) { _logs = logs; }

    public async Task HandleAsync(UserLoggedOut @event, CancellationToken ct = default)
    {
        var input = new AppendOpLogInput
        {
            Action = "POST",
            Resource = "/api/auth/logout",
            ModuleCN = "认证",
            FeatureCN = "退出",
            ReviewCN = "认证-退出-成功",
            Result = "OK",
            CreatedAt = DateTime.Now,
            CreatedBy = @event.UserId,
            Ip = @event.Ip
        };
        await _logs.AppendAsync(input, ct);
    }
}

