// 文件功能说明：
// IDialectRegistry 的默认实现。按方言代码（大小写不敏感）索引所有 IDatabaseDialect 实例。
// 注入时通过 IEnumerable<IDatabaseDialect> 自动收集所有 Singleton 注册的方言实现。

using Ginkgo.Infrastructure.Abstractions;

namespace Ginkgo.Infrastructure.Dialects;

/// <summary>
/// 数据库方言注册中心默认实现。
/// </summary>
public sealed class DialectRegistry : IDialectRegistry
{
    private readonly Dictionary<string, IDatabaseDialect> _byCode;

    /// <summary>
    /// 通过 DI 注入所有 <see cref="IDatabaseDialect"/> 实现来构建注册表。
    /// </summary>
    public DialectRegistry(IEnumerable<IDatabaseDialect> dialects)
    {
        _byCode = new Dictionary<string, IDatabaseDialect>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in dialects ?? Array.Empty<IDatabaseDialect>())
        {
            if (d == null) continue;
            if (string.IsNullOrWhiteSpace(d.Code))
                throw new InvalidOperationException($"方言实现 {d.GetType().FullName} 的 Code 不能为空");
            // 同一 Code 后注册覆盖前注册（允许下游用自定义 Dialect 替换内置实现）
            _byCode[d.Code] = d;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<DialectDescriptor> List()
        => _byCode.Values.Select(d => d.Descriptor).ToList();

    /// <inheritdoc/>
    public IDatabaseDialect Get(string code)
    {
        if (TryGet(code, out var d) && d != null) return d;
        var available = string.Join(", ", _byCode.Keys);
        throw new InvalidOperationException(
            $"不支持的数据库提供者: '{code}'。已注册的方言: [{available}]。" +
            $"请检查 Database:Provider 配置，或注册对应的 IDatabaseDialect 实现。");
    }

    /// <inheritdoc/>
    public bool TryGet(string code, out IDatabaseDialect? dialect)
    {
        if (string.IsNullOrWhiteSpace(code)) { dialect = null; return false; }
        return _byCode.TryGetValue(code.Trim(), out dialect);
    }
}
