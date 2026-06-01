// 文件功能说明：
// 统一客户端入口（Portal）API 控制器，提供 GET /api/v1/client/portal 接口，
// 供 WEB_PORTAL / UNIAPP / WPF 三端复用，返回该端默认菜单组下当前用户可见的入口树。
// 本接口标注 [LoginOnly]，仅要求已登录即可访问，跳过后台菜单资源权限映射；
// 与 GET /api/v1/menus/tree/my（后台 RBAC 菜单树）严格区分，仅读取 MenuGroupItem。

using Ginkgo.Application.Menus;
using Ginkgo.Plugin.Abstractions;
using Ginkgo.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ginkgo.Api.Controllers;

/// <summary>
/// 统一客户端入口接口。
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/client")]
[Authorize(Policy = "Permission")]
[ApiVersion("1.0")]
public sealed class ClientPortalController : ControllerBase
{
    private readonly IMenuGroupAppService _service;

    /// <summary>
    /// 受理的对外终端类型取值（大小写不敏感）。归一化由应用服务内部处理：
    /// MOBILE→UNIAPP、WPF→WPF、WEB_PORTAL→WEB_PORTAL。
    /// </summary>
    private static readonly HashSet<string> AllowedClientTypes =
        new(StringComparer.OrdinalIgnoreCase) { "MOBILE", "WPF", "WEB_PORTAL" };

    public ClientPortalController(IMenuGroupAppService service)
    {
        _service = service;
    }

    /// <summary>
    /// 从 JWT Claims 中提取当前用户 Id（与既有控制器一致，复用统一鉴权链路，不新建解析逻辑）。
    /// </summary>
    private long? GetCurrentUserId()
    {
        var uid = User.Claims.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier") || c.Type.EndsWith("/sub"))?.Value;
        if (long.TryParse(uid, out var userId)) return userId;
        return null;
    }

    /// <summary>
    /// 获取当前用户在指定终端类型下可见的统一入口树。
    /// 缺失或非法 clientType 返回 400；未登录由鉴权管道返回 401（不进入本方法）。
    /// </summary>
    /// <param name="clientType">终端类型，取值 MOBILE / WPF / WEB_PORTAL（大小写不敏感）。</param>
    /// <param name="ct">取消令牌。</param>
    [HttpGet("portal")]
    [LoginOnly]
    public async Task<Result<ClientPortalDto>> GetPortalAsync([FromQuery] string? clientType, CancellationToken ct)
    {
        // 1. 校验 clientType：缺失或不在受理取值集合内 → 返回参数错误（需求 9.1/9.8）
        if (string.IsNullOrWhiteSpace(clientType) || !AllowedClientTypes.Contains(clientType.Trim()))
        {
            return Result<ClientPortalDto>.Fail(400, "缺少或非法的 clientType 参数");
        }

        // 2. 由 JWT Claims 解析 userId（复用既有控制器方式）
        var userId = GetCurrentUserId();

        // 3. 调用应用服务构建入口树（clientType→内部终端类型的归一化在服务内部完成），包装为成功结果
        var data = await _service.GetClientPortalAsync(clientType.Trim(), userId, ct);
        return Result<ClientPortalDto>.Success(data);
    }
}
