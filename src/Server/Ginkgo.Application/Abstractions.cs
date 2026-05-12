// 文件功能说明：
// 定义应用层服务接口的通用抽象：标记接口、只读服务、CRUD 服务。

using Ginkgo.Domain;

namespace Ginkgo.Application;

/// <summary>
/// 应用服务标记接口。
/// </summary>
public interface IAppService { }

/// <summary>
/// 只读应用服务接口。
/// </summary>
/// <typeparam name="TDto">返回的 DTO 类型。</typeparam>
public interface IReadOnlyService<TDto>
{
    /// <summary>
    /// 根据主键获取单项数据。
    /// </summary>
    /// <param name="id">主键。</param>
    /// <param name="ct">取消令牌。</param>
    Task<TDto?> GetAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// 标准 CRUD 应用服务接口。
/// </summary>
/// <typeparam name="TDto">查询与返回的 DTO 类型。</typeparam>
/// <typeparam name="TCreate">创建输入类型。</typeparam>
/// <typeparam name="TUpdate">更新输入类型。</typeparam>
public interface ICrudService<TDto, in TCreate, in TUpdate> : IReadOnlyService<TDto>
{
    /// <summary>
    /// 新建数据。
    /// </summary>
    /// <param name="input">创建输入。</param>
    /// <param name="ct">取消令牌。</param>
    Task<Guid> CreateAsync(TCreate input, CancellationToken ct = default);

    /// <summary>
    /// 更新数据。
    /// </summary>
    /// <param name="id">主键。</param>
    /// <param name="input">更新输入。</param>
    /// <param name="ct">取消令牌。</param>
    Task UpdateAsync(Guid id, TUpdate input, CancellationToken ct = default);

    /// <summary>
    /// 删除数据。
    /// </summary>
    /// <param name="id">主键。</param>
    /// <param name="ct">取消令牌。</param>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}


