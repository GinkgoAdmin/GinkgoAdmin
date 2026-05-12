using Ginkgo.Domain.Events;

namespace Ginkgo.Domain.Users.Events;

/// <summary>
/// 用户登录成功领域事件。
/// </summary>
public sealed class UserLoggedIn : IDomainEvent
{
    public UserLoggedIn(long userId, string? userName, string? ip)
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
