// 文件功能说明：
// Reportable 报表查询入口的工厂抽象。统一封装 SqlSugar 的 db.Reportable<T>(list) API，
// 受 db.json 的 Database.Features.Reportable.Enabled 开关控制。
//
// 设计要点：
// - Enabled=false：调用 Create<T> 抛 NotSupportedException，强制业务侧显式启用；
//   业务侧也可先检查 IsEnabled 决定是否走 Reportable 路径。
// - Enabled=true：返回 SqlSugar 原生 IReportable<T>，调用方继续使用 .ToTable / .ToPivotTable
//   等链式 API 完成行列转置、动态分组、多维聚合。
// - 仅对内存数据（List<T>）做透视聚合；如需"查库 + 报表"组合，业务侧先 Queryable.ToListAsync()
//   再交给本工厂，避免与方言耦合。

using SqlSugar;

namespace Ginkgo.Infrastructure.Abstractions;

/// <summary>
/// 报表查询入口工厂。包装 SqlSugar 的 <c>db.Reportable&lt;T&gt;(list)</c>。
/// <c>Database.Features.Reportable.Enabled=false</c> 时 <see cref="Create{T}"/> 抛异常。
/// </summary>
public interface IReportableQueryFactory
{
    /// <summary>是否启用 Reportable 入口。</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// 基于内存数据创建 Reportable 查询入口。Disabled 时抛 <see cref="NotSupportedException"/>。
    /// </summary>
    /// <typeparam name="T">数据元素类型。</typeparam>
    /// <param name="data">已查询出的内存数据列表（不可为 null）。</param>
    /// <returns>SqlSugar 原生 <see cref="IReportable{T}"/>，调用 <c>.ToTable / .ToPivotTable</c> 等继续。</returns>
    IReportable<T> Create<T>(List<T> data);
}
