// 文件功能说明：
// 分表操作上下文抽象。封装 SqlSugar 的 SplitTable API，统一门控开关，让业务侧不必在每个
// Insert/Query/Update/Delete 链路上手动附加 .SplitTable()，降低遗漏风险。
//
// 设计要点：
// - 通过 db.json 的 Database.Features.SplitTable.Enabled 开关控制；Enabled=false 时
//   所有方法抛 NotSupportedException，业务侧应使用普通 CRUD。
// - Enabled=true 时，Insert/Update/Delete 自动附加 .SplitTable()；
//   QueryByRange 返回 ISugarQueryable<T> 已附带 .SplitTable(start, end)。
// - 实体必须配合 SqlSugar 的 [SplitTable(SplitType.xxx)] + [SplitField] 特性。
// - 本接口不负责建表；SqlSugar 会在首次 Insert 时按策略自动创建分区表。

using SqlSugar;
using System.Linq.Expressions;

namespace Ginkgo.Infrastructure.Abstractions;

/// <summary>
/// 分表操作上下文。统一封装 SqlSugar <c>.SplitTable()</c> 链式调用。
/// <c>Database.Features.SplitTable.Enabled=false</c> 时所有操作抛 <see cref="NotSupportedException"/>。
/// </summary>
public interface ISplitTableContext
{
    /// <summary>是否启用分表能力。</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// 分表插入单条实体。自动路由到正确的分区表。
    /// </summary>
    Task<int> InsertAsync<T>(T entity) where T : class, new();

    /// <summary>
    /// 分表批量插入。自动路由到正确的分区表。
    /// </summary>
    Task<int> InsertAsync<T>(List<T> entities) where T : class, new();

    /// <summary>
    /// 分表范围查询入口。返回已附带 <c>.SplitTable(start, end)</c> 的 <see cref="ISugarQueryable{T}"/>，
    /// 调用方可继续 <c>.Where(...).OrderBy(...).ToListAsync()</c>。
    /// </summary>
    ISugarQueryable<T> QueryByRange<T>(DateTime start, DateTime end) where T : class, new();

    /// <summary>
    /// 分表更新单条实体。自动路由到正确的分区表。
    /// </summary>
    Task<int> UpdateAsync<T>(T entity) where T : class, new();

    /// <summary>
    /// 分表条件删除。在所有分区内删除满足 <paramref name="predicate"/> 条件的记录。
    /// 建议 predicate 中包含时间范围条件以缩小扫描范围。
    /// </summary>
    Task<int> DeleteAsync<T>(Expression<Func<T, bool>> predicate) where T : class, new();
}
