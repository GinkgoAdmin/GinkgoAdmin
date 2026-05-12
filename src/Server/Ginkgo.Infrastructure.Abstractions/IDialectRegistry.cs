// 文件功能说明：
// 数据库方言注册中心契约。由 Ginkgo.Infrastructure 提供实现（DialectRegistry），按 Code
// （小写）索引所有 IDatabaseDialect 实例。配置 Database:Provider 通过此注册中心解析为
// 具体方言实例，未注册时抛出明确异常（错误信息包含已注册清单）。

namespace Ginkgo.Infrastructure.Abstractions;

/// <summary>
/// 数据库方言注册中心。
/// </summary>
public interface IDialectRegistry
{
    /// <summary>
    /// 列出当前注册的所有方言描述符（供前端 / API 暴露）。
    /// </summary>
    IReadOnlyList<DialectDescriptor> List();

    /// <summary>
    /// 按方言代码（大小写不敏感）获取方言实例。未找到时抛 <see cref="InvalidOperationException"/>，
    /// 异常信息中包含已注册的方言代码清单，便于排错。
    /// </summary>
    IDatabaseDialect Get(string code);

    /// <summary>
    /// 尝试按方言代码获取方言实例，未找到不抛异常，返回 false 与 null。
    /// </summary>
    bool TryGet(string code, out IDatabaseDialect? dialect);
}
