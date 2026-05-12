using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using Ginkgo.Plugin.Abstractions;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Ginkgo.Api.Services;

/// <summary>
/// 接口注释（标题）目录（单例）。
/// <para>
/// 扫描当前运行时所有 MVC Action，读取 <see cref="EndpointCommentAttribute"/> 标注，
/// 建立 <c>(HTTP Method, 路由模板)</c> → 接口标题 的内存字典，供运维、监控、审计等横切能力
/// 按 <c>Method + Path</c> 查询对应接口的简短标题。
/// </para>
/// <para>
/// 设计要点：
/// <list type="bullet">
///   <item>基于 <see cref="IActionDescriptorCollectionProvider"/> 的 <c>Version</c> 字段感知
///         模块热加载，版本变化时自动重建索引。</item>
///   <item>查询入参是具体请求路径（如 <c>/api/resource-monitor/modules/abc/trend</c>），
///         内部会与路由模板（含 <c>{moduleId}</c> 占位符）做模板匹配。</item>
///   <item>Action 未标注但 Controller 标注时，取 Controller 级描述作为兜底。</item>
///   <item>查询完全为空时返回 <c>null</c>，调用方应按"没有则不显示"处理，不要抛异常。</item>
/// </list>
/// </para>
/// </summary>
public sealed class EndpointDescriptionCatalog
{
    private readonly IActionDescriptorCollectionProvider _provider;
    private volatile IReadOnlyList<EndpointCommentEntry> _entries = Array.Empty<EndpointCommentEntry>();
    private int _snapshotVersion = -1;
    private readonly object _rebuildLock = new();

    public EndpointDescriptionCatalog(IActionDescriptorCollectionProvider provider)
    {
        _provider = provider;
    }

    /// <summary>
    /// 按 HTTP 方法 + 请求路径查询接口注释。找不到时返回 null。
    /// </summary>
    /// <param name="method">HTTP 方法，大小写不敏感；传空视为忽略方法过滤。</param>
    /// <param name="path">请求路径，例如 <c>/api/resource-monitor/modules/abc/trend</c>。</param>
    public EndpointCommentEntry? Resolve(string? method, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        EnsureLatest();

        var normalizedPath = NormalizePath(path);
        var methodUpper = string.IsNullOrWhiteSpace(method) ? null : method.Trim().ToUpperInvariant();

        EndpointCommentEntry? fallback = null;
        foreach (var entry in _entries)
        {
            if (methodUpper != null && entry.HttpMethods.Count > 0 &&
                !entry.HttpMethods.Contains(methodUpper))
                continue;

            if (!entry.PathRegex.IsMatch(normalizedPath)) continue;

            // 方法精确匹配优先返回
            if (methodUpper != null && entry.HttpMethods.Contains(methodUpper))
                return entry;

            fallback ??= entry;
        }
        return fallback;
    }

    /// <summary>
    /// 批量查询。输入 (method, path) 列表，输出等长的结果列表（无标题则对应项为 null）。
    /// </summary>
    public IReadOnlyList<EndpointCommentEntry?> ResolveBatch(IEnumerable<(string? Method, string? Path)> queries)
    {
        var list = new List<EndpointCommentEntry?>();
        foreach (var q in queries)
        {
            list.Add(Resolve(q.Method, q.Path));
        }
        return list;
    }

    private void EnsureLatest()
    {
        var collection = _provider.ActionDescriptors;
        if (collection.Version == _snapshotVersion) return;

        lock (_rebuildLock)
        {
            if (collection.Version == _snapshotVersion) return;
            _entries = BuildEntries(collection.Items);
            _snapshotVersion = collection.Version;
        }
    }

    private static IReadOnlyList<EndpointCommentEntry> BuildEntries(IEnumerable<ActionDescriptor> actions)
    {
        var list = new List<EndpointCommentEntry>();
        foreach (var action in actions)
        {
            if (action is not ControllerActionDescriptor cad) continue;
            var route = cad.AttributeRouteInfo?.Template;
            if (string.IsNullOrWhiteSpace(route)) continue;

            var actionDesc = cad.MethodInfo.GetCustomAttribute<EndpointCommentAttribute>(inherit: true);
            var controllerDesc = cad.ControllerTypeInfo.GetCustomAttribute<EndpointCommentAttribute>(inherit: true);
            var chosen = actionDesc ?? controllerDesc;
            if (chosen == null) continue; // 完全没有注释的接口不进目录

            var httpMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var meta in cad.EndpointMetadata ?? Array.Empty<object>())
            {
                if (meta is Microsoft.AspNetCore.Routing.IHttpMethodMetadata hm)
                {
                    foreach (var m in hm.HttpMethods)
                        httpMethods.Add(m.ToUpperInvariant());
                }
            }

            list.Add(new EndpointCommentEntry(
                template: route,
                httpMethods: httpMethods,
                pathRegex: BuildPathRegex(route),
                description: chosen.Description,
                category: chosen.Category,
                fromController: actionDesc == null));
        }
        return list;
    }

    /// <summary>
    /// 把 ASP.NET 路由模板（<c>api/x/{id}</c>）转为匹配具体请求路径的正则。
    /// </summary>
    private static Regex BuildPathRegex(string template)
    {
        var t = template.Trim('/');
        // 统一把路由参数（含约束）替换为路径段占位符
        var pattern = Regex.Replace(t, @"\{[^/]+?\}", "[^/]+");
        pattern = "^/" + pattern + "/?$";
        return new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        var trimmed = path.Trim();
        // 去掉 querystring
        var q = trimmed.IndexOf('?');
        if (q >= 0) trimmed = trimmed.Substring(0, q);
        if (!trimmed.StartsWith('/')) trimmed = "/" + trimmed;
        return trimmed;
    }
}

/// <summary>
/// 接口注释（标题）条目（内存索引的一行）。
/// </summary>
public sealed class EndpointCommentEntry
{
    /// <summary>
    /// 原始路由模板（含占位符），如 <c>api/resource-monitor/modules/{moduleId}/trend</c>。
    /// </summary>
    public string Template { get; }
    /// <summary>
    /// 该路由接受的 HTTP 方法集合（大写）。空集合表示任意方法。
    /// </summary>
    public IReadOnlySet<string> HttpMethods { get; }
    /// <summary>
    /// 接口标题（面向运维的短标题，勿写成长说明）。
    /// </summary>
    public string Description { get; }
    /// <summary>
    /// 可选分类标签。
    /// </summary>
    public string? Category { get; }
    /// <summary>
    /// 标题是否来自 Controller 级的兜底而非 Action 级。
    /// </summary>
    public bool FromController { get; }

    internal Regex PathRegex { get; }

    internal EndpointCommentEntry(string template, HashSet<string> httpMethods, Regex pathRegex,
        string description, string? category, bool fromController)
    {
        Template = template;
        HttpMethods = httpMethods;
        PathRegex = pathRegex;
        Description = description;
        Category = category;
        FromController = fromController;
    }
}
