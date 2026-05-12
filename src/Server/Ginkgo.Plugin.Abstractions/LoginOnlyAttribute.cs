namespace Ginkgo.Plugin.Abstractions;

/// <summary>
/// 标记控制器方法（或整个控制器）为"仅登录即可访问"。
/// <para>
/// 被此特性标记的端点不需要在菜单表中配置 Resource+Method 权限映射，
/// 也不需要在主框架的白名单中注册。只要用户已认证（已登录）即可通过权限检查。
/// </para>
/// <para>
/// 适用场景：当前用户操作自己数据的接口、公共 lookup / 下拉数据查询等。
/// 管理类接口不应使用此特性，应通过菜单权限体系控制。
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class LoginOnlyAttribute : Attribute
{
}
