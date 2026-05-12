// 文件功能说明：
// 定义"用户注册完成"领域事件（DDD），用于触发后置初始化（默认角色/部门）。

using Ginkgo.Domain.Events;

namespace Ginkgo.Domain.Users.Events;

/// <summary>
/// 用户注册完成事件。
/// </summary>
public sealed class UserRegistered : IDomainEvent
{
    public UserRegistered(long userId)
    {
        UserId = userId;
        OccurredOn = DateTime.Now;
    }

    public long UserId { get; }
    public DateTime OccurredOn { get; }
}
