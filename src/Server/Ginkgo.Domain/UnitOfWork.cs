namespace Ginkgo.Domain;

/// <summary>
/// 工作单元抽象：用于在应用用例（命令处理器）中定义事务边界。
/// </summary>
public interface IUnitOfWork
{
    Task BeginAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

