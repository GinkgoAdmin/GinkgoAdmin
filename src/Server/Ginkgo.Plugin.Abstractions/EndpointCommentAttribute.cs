namespace Ginkgo.Plugin.Abstractions;

/// <summary>
/// 可选的"接口注释（标题）"特性。
/// <para>
/// <b>这是一个短标题，不是长说明。</b>用一句话概括接口能力即可，建议 6–15 字、最多 20 字以内，
/// 例如"查询客户档案"、"颁发 OAuth 令牌"、"更新监控阈值"。运维在监控/审计页面里看到 URL 后，
/// 立刻能从这条标题判断"这个接口是做什么的"，无需再翻源码或追开发；调用细节、参数说明、
/// 业务流程一律不要塞进来。
/// </para>
/// <para>
/// 标注位置：可标在 <b>Controller 类</b> 或 <b>Action 方法</b> 上。Action 上的标题优先于 Controller，
/// Controller 上的标题作为该控制器下未单独标注接口的兜底。
/// </para>
/// <para>
/// 与 .NET 内置的 <c>Microsoft.AspNetCore.Http.EndpointDescriptionAttribute</c> 不同：
/// 后者主要用于 OpenAPI 工具链生成完整接口文档；本特性是 Ginkgo 专为运维场景定义的
/// "接口注释（标题）"扩展，命名为 <c>EndpointComment</c> 以避免与内置类型混淆，且额外提供
/// <see cref="Category"/> 运维分类标签，便于面板着色和筛选。
/// </para>
/// <para>
/// 使用规则：
/// <list type="number">
///   <item>完全可选：未标注的接口不会有任何行为变化，调用方也不强制依赖。</item>
///   <item>只写"是什么"，不写"怎么做"；写成短标题，不要写成段落。</item>
///   <item>多端共用：插件 Web 端、UniApp 端、WPF 端各插件在对接同一条 API 时共享同一份标题，无需重复维护。</item>
///   <item>面向运维：用中文业务语言，不要贴英文函数名、变量名或 HTTP 协议细节。</item>
/// </list>
/// </para>
/// <para>
/// 典型用法：
/// <code>
/// [HttpGet("customers/{id}")]
/// [EndpointComment("查询客户档案", Category = "只读")]
/// public async Task&lt;IActionResult&gt; GetCustomer(long id) { ... }
/// </code>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class EndpointCommentAttribute : Attribute
{
    /// <summary>
    /// 接口标题（面向运维的短标题，建议 6–15 字，最长不超过 20 字；不要写成长说明）。
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 可选的分类标签，例如 "只读"、"会写库"、"高危操作"、"后台管理"、"门户自助" 等。
    /// 运维面板可以据此着色或筛选。不填则视为无特殊标签。
    /// </summary>
    public string? Category { get; set; }

    public EndpointCommentAttribute(string description)
    {
        Description = description ?? string.Empty;
    }
}
