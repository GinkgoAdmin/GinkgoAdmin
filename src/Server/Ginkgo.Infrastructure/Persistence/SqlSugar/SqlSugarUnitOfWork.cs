using Ginkgo.Domain;
using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.SqlSugar;

/// <summary>
/// UnitOfWork implementation for SqlSugar
/// </summary>
public sealed class SqlSugarUnitOfWork : IUnitOfWork
{
    private readonly ISqlSugarClient _db;
    public SqlSugarUnitOfWork(ISqlSugarClient db) => _db = db;

    public Task BeginAsync(CancellationToken ct = default)
    {
        _db.Ado.BeginTran();
        return Task.CompletedTask;
    }

    public Task CommitAsync(CancellationToken ct = default)
    {
        _db.Ado.CommitTran();
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken ct = default)
    {
        _db.Ado.RollbackTran();
        return Task.CompletedTask;
    }
}

