// 文件功能说明：
// IReportableQueryFactory 的默认实现。委托 SqlSugar 原生 db.Reportable<T>(list)。
// Enabled=false 时调用 Create 抛 NotSupportedException，提示在 db.json 启用 Reportable 开关。

using Ginkgo.Infrastructure.Abstractions;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.Features;

/// <summary>
/// Reportable 报表查询入口默认实现。
/// </summary>
public sealed class ReportableQueryFactory : IReportableQueryFactory
{
    private readonly ISqlSugarClient _client;
    private readonly ReportableOptions _options;

    public ReportableQueryFactory(
        ISqlSugarClient client,
        IOptions<DatabaseFeaturesOptions> features)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = features?.Value?.Reportable ?? new ReportableOptions();
    }

    /// <summary>供单测注入自定义选项。</summary>
    internal ReportableQueryFactory(ISqlSugarClient client, ReportableOptions options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? new ReportableOptions();
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.Enabled;

    /// <inheritdoc />
    public IReportable<T> Create<T>(List<T> data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        if (!_options.Enabled)
        {
            throw new NotSupportedException(
                "Reportable 报表入口未启用。请在 db.json 设置 Database.Features.Reportable.Enabled = true 后再使用。" +
                "未启用时业务侧应继续使用自定义 Queryable / 内存 LINQ 完成聚合，避免引入额外抽象。");
        }

        return _client.Reportable(data);
    }
}
