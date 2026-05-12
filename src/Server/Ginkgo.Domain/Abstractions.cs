// 文件功能说明：
// 定义领域层的基础抽象：通用实体、审计实体与泛型仓储接口。

using SqlSugar;
using Ginkgo.Domain.Utils;

namespace Ginkgo.Domain;

/// <summary>
/// 领域实体基类，包含统一的主键 Id。
/// </summary>
public abstract class Entity
{
    /// <summary>
    /// 实体主键（Snowflake ID）。
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, ColumnName = "Id", ColumnDataType = "bigint", ColumnDescription = "实体主键Id（Snowflake ID）")]
    public long Id { get; set; } = SnowflakeIdGenerator.NextId();
}

/// <summary>
/// 审计实体基类，包含创建与更新信息，以及软删除标记。
/// </summary>
public abstract class AuditableEntity : Entity
{
    /// <summary>
    /// 创建时间（UTC）。
    /// </summary>
    [SugarColumn(ColumnDescription = "创建时间（UTC），默认当前时间")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 创建人用户 ID（Snowflake ID）。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "创建人用户Id（可空，Snowflake ID）")]
    public long? CreatedBy { get; set; }

    /// <summary>
    /// 最后更新时间（UTC）。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "最后更新时间（可空）")]
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 最后更新人用户 ID（Snowflake ID）。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "最后更新人用户Id（可空，Snowflake ID）")]
    public long? UpdatedBy { get; set; }

    /// <summary>
    /// 是否软删除。
    /// </summary>
    [SugarColumn(ColumnDescription = "是否软删除标记（true 为删除，不参与业务查询）")]
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 删除时间（UTC，可空）。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "删除时间（可空）")]
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// 删除人用户 ID（Snowflake ID，可空）。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "删除人用户Id（可空，Snowflake ID）")]
    public long? DeletedBy { get; set; }
}

/// <summary>
/// 泛型仓储接口，定义最小的数据访问操作。
/// </summary>
/// <typeparam name="T">实体类型。</typeparam>
public interface IRepository<T> where T : Entity
{
    /// <summary>
    /// 返回可查询的集合，用于组合查询。
    /// </summary>
    ISugarQueryable<T> Query();

    /// <summary>
    /// 根据主键获取实体。
    /// </summary>
    /// <param name="id">实体主键（Snowflake ID）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增实体。
    /// </summary>
    /// <param name="entity">实体对象。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新实体。
    /// </summary>
    /// <param name="entity">实体对象。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据主键删除实体。
    /// </summary>
    /// <param name="id">实体主键（Snowflake ID）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量新增实体。
    /// </summary>
    /// <param name="entities">实体集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量更新实体。
    /// </summary>
    /// <param name="entities">实体集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量删除实体。
    /// </summary>
    /// <param name="ids">实体主键集合（Snowflake ID）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task DeleteRangeAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取实体总数。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<long> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查实体是否存在。
    /// </summary>
    /// <param name="id">实体主键（Snowflake ID）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有实体。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
}


