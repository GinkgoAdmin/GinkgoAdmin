// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）属性测试用的内存版 IRepository<T> 实现。
// 该实现基于 InMemoryTestDatabase 的 SQLite 内存库，复刻真实 SqlSugarRepository<T> 的关键语义：
//   - Query() 对审计实体自动过滤软删除（IsDeleted=false），返回真实 ISugarQueryable<T> 供 LINQ 查询；
//   - AddAsync/AddRangeAsync 写入并补齐 CreatedAt；
//   - UpdateAsync/UpdateRangeAsync 更新并补齐 UpdatedAt；
//   - DeleteAsync/DeleteRangeAsync 对审计实体执行软删除、对普通实体执行物理删除；
//   - GetByIdAsync/ExistsAsync/CountAsync/GetAllAsync 均遵循软删除过滤。
// 租户上下文行为模拟：每个 InMemoryTestDatabase 实例对应一份相互隔离的内存库，
//   不同上下文之间数据天然不可见，等价于框架既有的多租户（按库隔离）链路。

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ginkgo.Domain;
using SqlSugar;

namespace Ginkgo.Application.Tests.Infrastructure;

/// <summary>
/// 内存版泛型仓储，供应用服务纯逻辑属性测试注入。
/// </summary>
/// <typeparam name="T">实体类型（必须含无参构造，满足 SqlSugar 物化要求）。</typeparam>
public sealed class InMemoryRepository<T> : IRepository<T> where T : Entity, new()
{
    private readonly ISqlSugarClient _db;

    /// <summary>
    /// 以内存测试数据库上下文构造仓储。
    /// </summary>
    public InMemoryRepository(InMemoryTestDatabase database)
    {
        _db = database.Client;
    }

    private static bool IsAuditable => typeof(AuditableEntity).IsAssignableFrom(typeof(T));

    /// <summary>
    /// 返回可查询集合；审计实体自动过滤软删除记录。
    /// </summary>
    public ISugarQueryable<T> Query()
    {
        var q = _db.Queryable<T>();
        if (IsAuditable)
        {
            q = q.Where("IsDeleted = @IsDeleted", new { IsDeleted = false });
        }
        return q;
    }

    /// <summary>
    /// 按主键获取实体（遵循软删除过滤）。
    /// </summary>
    public async Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var q = _db.Queryable<T>().Where(x => x.Id == id);
        if (IsAuditable)
        {
            q = q.Where("IsDeleted = @IsDeleted", new { IsDeleted = false });
        }
        return await q.FirstAsync();
    }

    /// <summary>
    /// 新增实体，补齐审计字段。
    /// </summary>
    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity is AuditableEntity aud)
        {
            if (aud.CreatedAt == default) aud.CreatedAt = System.DateTime.Now;
            aud.IsDeleted = false;
        }
        await _db.Insertable(entity).ExecuteCommandAsync();
    }

    /// <summary>
    /// 更新实体，补齐更新时间；未命中记录抛异常（与真实仓储一致）。
    /// </summary>
    public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity is AuditableEntity aud)
        {
            aud.UpdatedAt = System.DateTime.Now;
        }
        var affected = await _db.Updateable(entity).Where(x => x.Id == entity.Id).ExecuteCommandAsync();
        if (affected == 0)
        {
            throw new System.InvalidOperationException($"更新失败：未找到要更新的记录（{typeof(T).Name}:{entity.Id}）");
        }
    }

    /// <summary>
    /// 删除实体：审计实体软删除、普通实体物理删除。
    /// </summary>
    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (IsAuditable)
        {
            var entity = await GetByIdAsync(id, cancellationToken);
            if (entity == null) return;
            var aud = (AuditableEntity)(object)entity;
            aud.IsDeleted = true;
            aud.UpdatedAt = System.DateTime.Now;
            aud.DeletedAt = System.DateTime.Now;
            await _db.Updateable((T)(object)aud).ExecuteCommandAsync();
        }
        else
        {
            await _db.Deleteable<T>().In(id).ExecuteCommandAsync();
        }
    }

    /// <summary>
    /// 批量新增实体，补齐审计字段。
    /// </summary>
    public async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var list = entities.ToList();
        if (list.Count == 0) return;
        foreach (var entity in list)
        {
            if (entity is AuditableEntity aud)
            {
                if (aud.CreatedAt == default) aud.CreatedAt = System.DateTime.Now;
                aud.IsDeleted = false;
            }
        }
        await _db.Insertable(list).ExecuteCommandAsync();
    }

    /// <summary>
    /// 批量更新实体，补齐更新时间。
    /// </summary>
    public async Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var list = entities.ToList();
        if (list.Count == 0) return;
        foreach (var entity in list)
        {
            if (entity is AuditableEntity aud)
            {
                aud.UpdatedAt = System.DateTime.Now;
            }
        }
        await _db.Updateable(list).ExecuteCommandAsync();
    }

    /// <summary>
    /// 批量删除实体：审计实体软删除、普通实体物理删除。
    /// </summary>
    public async Task DeleteRangeAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return;

        if (IsAuditable)
        {
            var entities = await _db.Queryable<T>().Where(x => idList.Contains(x.Id)).ToListAsync();
            foreach (var entity in entities)
            {
                if (entity is AuditableEntity aud)
                {
                    aud.IsDeleted = true;
                    aud.UpdatedAt = System.DateTime.Now;
                    aud.DeletedAt = System.DateTime.Now;
                }
            }
            if (entities.Count > 0)
            {
                await _db.Updateable(entities).ExecuteCommandAsync();
            }
        }
        else
        {
            await _db.Deleteable<T>().In(idList).ExecuteCommandAsync();
        }
    }

    /// <summary>
    /// 统计实体总数（遵循软删除过滤）。
    /// </summary>
    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        var q = _db.Queryable<T>();
        if (IsAuditable)
        {
            q = q.Where("IsDeleted = @IsDeleted", new { IsDeleted = false });
        }
        return await q.CountAsync();
    }

    /// <summary>
    /// 判断实体是否存在（遵循软删除过滤）。
    /// </summary>
    public async Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default)
    {
        var q = _db.Queryable<T>().Where(x => x.Id == id);
        if (IsAuditable)
        {
            q = q.Where("IsDeleted = @IsDeleted", new { IsDeleted = false });
        }
        return await q.AnyAsync();
    }

    /// <summary>
    /// 获取全部实体（遵循软删除过滤）。
    /// </summary>
    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var q = _db.Queryable<T>();
        if (IsAuditable)
        {
            q = q.Where("IsDeleted = @IsDeleted", new { IsDeleted = false });
        }
        return await q.ToListAsync();
    }
}
