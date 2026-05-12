// 文件功能说明：
// 定义基础通知 Hub，通过 SignalR 推送系统消息与任务进度等事件。

using Microsoft.AspNetCore.SignalR;
using System;
using System.Security.Claims;

namespace Ginkgo.Realtime;

/// <summary>
/// 通知 Hub。
/// </summary>
public sealed class NotifyHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // 将用户加入各自的组，便于定向推送
        var uid = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                  Context.User?.FindFirst(c => c.Type.EndsWith("/sub"))?.Value;
        
        // 支持 Snowflake ID (long) 和 GUID 两种格式
        if (!string.IsNullOrWhiteSpace(uid))
        {
            // 优先尝试解析为 long（Snowflake ID）
            if (long.TryParse(uid, out var longUserId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{longUserId}");
                Console.WriteLine($"[NotifyHub] User {longUserId} joined group user:{longUserId}");
            }
            // 兼容 GUID 格式
            else if (Guid.TryParse(uid, out var guidUserId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{guidUserId}");
                Console.WriteLine($"[NotifyHub] User {guidUserId} joined group user:{guidUserId}");
            }
            else
            {
                // 直接使用原始字符串作为组名
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{uid}");
                Console.WriteLine($"[NotifyHub] User {uid} joined group user:{uid}");
            }
        }
        // 角色分组
        foreach (var role in Context.User?.FindAll(ClaimTypes.Role) ?? Array.Empty<Claim>())
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"role:{role.Value}");
        }
        // 部门分组（如有 deptId 声明）
        var dept = Context.User?.FindFirst("dept")?.Value;
        if (!string.IsNullOrWhiteSpace(dept))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"dept:{dept}");
        }
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// 向指定用户发送文本消息。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="message">消息内容。</param>
    public async Task SendToUserAsync(string userId, string message)
    {
        await Clients.User(userId).SendAsync("Notify.Message", message);
    }

    /// <summary>
    /// 广播消息到所有连接。
    /// </summary>
    /// <param name="message">消息内容。</param>
    public async Task BroadcastAsync(string message)
    {
        await Clients.All.SendAsync("Notify.Message", message);
    }
}






