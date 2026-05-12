namespace Ginkgo.Domain.Notifications;

/// <summary>
/// 领域服务：将受众种子解析为具体收件人。
/// 注意：最小实现可仅支持 TargetType=0（用户），后续再扩展角色/部门/表达式。
/// </summary>
public interface IAudienceResolver
{
    Task<IReadOnlyList<AudienceMember>> ResolveAsync(long notifyId, IEnumerable<AudienceSeed> seeds, CancellationToken ct = default);
}

