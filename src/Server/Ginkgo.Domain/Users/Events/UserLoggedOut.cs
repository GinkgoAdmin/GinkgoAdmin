using Ginkgo.Domain.Events;

namespace Ginkgo.Domain.Users.Events;

/// <summary>
/// 用户登出成功领域事件。
/// </summary>
public sealed class UserLoggedOut : IDomainEvent
{
    public UserLoggedOut(long userId, string? userName, string? ip)
    {
        UserId = userId;
        UserName = userName;
        Ip = ip;
        OccurredOn = DateTime.Now;
    }

    public long UserId { get; }
    public string? UserName { get; }
    public string? Ip { get; }
    public DateTime OccurredOn { get; }
}
