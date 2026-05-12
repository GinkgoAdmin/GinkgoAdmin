using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Ginkgo.Realtime;

/// <summary>
/// 基于 SignalR 的实时通知实现。
/// </summary>
public sealed class RealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<NotifyHub> _hub;

    public RealtimeNotifier(IHubContext<NotifyHub> hub)
    {
        _hub = hub;
    }

    public Task BroadcastAsync(string method, object payload)
        => _hub.Clients.All.SendAsync(method, payload);

    public Task SendToGroupAsync(string group, string method, object payload)
        => _hub.Clients.Group(group).SendAsync(method, payload);

    public Task SendToUserAsync(Guid userId, string method, object payload)
        => _hub.Clients.User(userId.ToString()).SendAsync(method, payload);

    public Task SendToUsersAsync(IEnumerable<Guid> userIds, string method, object payload)
        => _hub.Clients.Users(userIds.Select(x => x.ToString())).SendAsync(method, payload);
}


