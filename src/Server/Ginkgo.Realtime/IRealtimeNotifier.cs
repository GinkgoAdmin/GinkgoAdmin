using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ginkgo.Realtime;

/// <summary>
/// 实时通知抽象：为业务模块提供统一的推送入口。
/// </summary>
public interface IRealtimeNotifier
{
    Task SendToUserAsync(Guid userId, string method, object payload);
    Task SendToUsersAsync(IEnumerable<Guid> userIds, string method, object payload);
    Task SendToGroupAsync(string group, string method, object payload);
    Task BroadcastAsync(string method, object payload);
}


