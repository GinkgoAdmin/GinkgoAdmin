    using Ginkgo.Application.Settings;
using Ginkgo.Application.Files;
using Ginkgo.Shared;
using Ginkgo.ServerToolkit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Ginkgo.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Ginkgo.Api.Controllers;

/// <summary>
/// 系统配置接口。
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/settings")]
[ApiVersion("1.0")]
[Authorize(Policy = "Permission")]
public sealed class SettingsController : ControllerBase
{
    private const string PublicSettingsCacheKey = "settings:public-list";
    private readonly ISettingsAppService _service;
    private readonly IMemoryCache _memoryCache;
    public SettingsController(ISettingsAppService service, IMemoryCache memoryCache) { _service = service; _memoryCache = memoryCache; }

    /// <summary>
    /// 获取所有配置。
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<Result<List<SettingDto>>> GetAllAsync()
    {
        var list = await _memoryCache.GetOrCreateAsync(PublicSettingsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            return await _service.GetAllAsync();
        }) ?? new List<SettingDto>();
        // 如果非管理员，仅返回"前端可见的白名单"配置
        var uid = User?.Claims.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier") || c.Type.EndsWith("/sub"))?.Value;
        long.TryParse(uid, out var userId);
        bool isAdmin = await IsAdminAsync(userId);
        if (!isAdmin)
        {
            var allowedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Site.Name",
                "Site.Url",
                "Site.Logo",
                "Site.Maintenance.Enabled",
                "Site.Maintenance.Enable",
                "Site.DefaultLanguage",
                "Site.TimeZone",
                // 主题与品牌（web 前端 / 登录页需要展示）
                "Site.Branding.Favicon",
                "Site.Theme.PrimaryColor",
                "Site.Theme.SecondaryColor",
                // 登录页文案与背景
                "Site.Subtitle",
                "Site.Login.WelcomeText",
                "Site.Login.LeftPanelBackground",
                // 动画
                "Site.Animation.Enabled",
                "Site.Animation.Intensity",
                // 页脚 + ICP / 公安备案（web 前端公开展示）
                "Site.Footer.Text",
                "Site.ICP",
                "Site.PoliceICP",
                "Site.BusinessLicense",
                // SEO
                "Site.SEO.Keywords",
                "Site.SEO.Description",
                "Mail.From",
                "Mail.DisplayName",
                // 登录与前端需要读取注册开关及模式
                "Registration.Enabled",
                "Registration.Mode",
                "Registration.LoginMethods",
                "Registration.LoginCaptcha",
                // 客户端与前端需要读取通知音频配置
                "Notification.AudioUrl",
                "Notification.Audio.Url",
                "Notification.Audio.Enabled",
                // 允许前端读取 CodeDesigner 模块路径配置
                "cudr.modulepath",
                // 上传配置（前端组件需要读取）
                "Upload.MaxSizeMB",
                "Upload.AllowedExtensions",
                "Upload.BasePath",
                "Upload.ImageCompress.Enabled",
                "Upload.ImageCompress.Quality",
                "Upload.ImageCompress.KeepOriginal",
                // 移动端（UNIAPP）公开配置
                "App.HomePlugin",
                "App.UniappHomePath",
                "App.Privacy.ShowPopup",
                "App.Privacy.PolicyVersion",
                "App.Privacy.PolicyContent",
                "App.Privacy.UserAgreementContent",
                "App.Privacy.EnableCorrectInfo",
                "App.Privacy.EnableDeleteAccount",
                "App.Privacy.EnableWithdrawConsent"
            };
            list = list?.Where(s => s?.Key != null && (allowedKeys.Contains(s.Key) || s.Key.StartsWith("cudr.", StringComparison.OrdinalIgnoreCase) || s.Key.StartsWith("Language.MultiLang.", StringComparison.OrdinalIgnoreCase) || s.Key.StartsWith("App.", StringComparison.OrdinalIgnoreCase))).ToList() ?? new List<SettingDto>();
        }
        return Result<List<SettingDto>>.Success(list ?? new List<SettingDto>());
        }


        /// <summary>
        /// 获取全部配置（管理端使用）。需要授权。
        /// </summary>
        [HttpGet("all")]
        [Authorize]
        public async Task<Result<List<SettingDto>>> GetAllForAdminAsync()
        {
            var list = await _service.GetAllAsync();
            return Result<List<SettingDto>>.Success(list ?? new List<SettingDto>());
        }

    private async Task<bool> IsAdminAsync(long userId)                                      
    {
        if (userId == 0) return false;
        try
        {
            var userRoleRepo = HttpContext.RequestServices.GetRequiredService<IRepository<Ginkgo.Domain.Users.UserRole>>();
            // 管理员角色固定 Id：1 (Snowflake ID for admin role)
            var adminId = 1L;
            var has = await userRoleRepo.Query().AnyAsync(ur => ur.UserId == userId && ur.RoleId == adminId);
            return has;
        }
        catch { return false; }
    }

    /// <summary>
    /// 批量新增或更新配置。
    /// </summary>
    [HttpPost("batch")]
    public async Task<Result> UpsertBatchAsync([FromBody] List<SettingDto> inputs)
    {
        if (inputs == null || inputs.Count == 0)
        {
            return Result.Fail(400, "配置列表不能为空");
        }

        long? userId = null;
        var uid = User?.Claims.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier") || c.Type.EndsWith("/sub"))?.Value;
        if (long.TryParse(uid, out var gid)) userId = gid;

        try
        {
            // 批量保存
            foreach (var input in inputs)
            {
                await _service.UpsertAsync(input, userId);
            }

            _memoryCache.Remove(PublicSettingsCacheKey);
        }
        catch (InvalidOperationException)
        {
            return Result.Fail(409, "配置已被其他人修改，请刷新后重试");
        }
        catch (Exception ex)
        {
            return Result.Fail(500, $"批量保存失败：{ex.Message}");
        }
        return Result.Success($"批量保存成功，共处理 {inputs.Count} 项配置");
    }

    /// <summary>
    /// 新增或更新配置。
    /// </summary>
    [HttpPost]
    public async Task<Result> UpsertAsync([FromBody] SettingDto input)
    {
        long? userId = null;
        var uid = User?.Claims.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier") || c.Type.EndsWith("/sub"))?.Value;
        if (long.TryParse(uid, out var gid)) userId = gid;
        try
        {
            await _service.UpsertAsync(input, userId);
            _memoryCache.Remove(PublicSettingsCacheKey);
        }
        catch (InvalidOperationException)
        {
            return Result.Fail(409, "配置已被其他人修改，请刷新后重试");
        }
        catch (Exception ex)
        {
            return Result.Fail(500, $"保存失败：{ex.Message}");
        }
        return Result.Success("保存成功");
    }

    /// <summary>
    /// 发送测试邮件。使用当前已保存的 SMTP 配置发送一封测试邮件到指定地址。
    /// </summary>
    [HttpPost("test-email")]
    public async Task<Result> SendTestEmailAsync([FromBody] TestEmailInput input)
    {
        if (string.IsNullOrWhiteSpace(input?.To))
        {
            return Result.Fail(400, "请输入收件人邮箱地址");
        }

        try
        {
            var emailSender = HttpContext.RequestServices.GetRequiredService<IEmailSender>();
            var message = new EmailMessage(
                To: input.To.Trim(),
                Subject: "Ginkgo 邮件配置测试",
                Body: $"""
                <div style="font-family: 'Microsoft YaHei', Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 30px;">
                    <h2 style="color: #409eff;">邮件配置测试成功</h2>
                    <p>这是一封来自 <strong>Ginkgo 系统</strong> 的测试邮件。</p>
                    <p>如果您收到了这封邮件，说明系统的 SMTP 邮件配置已经正确设置。</p>
                    <hr style="border: none; border-top: 1px solid #eee; margin: 20px 0;" />
                    <p style="color: #999; font-size: 12px;">发送时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
                </div>
                """,
                IsHtml: true
            );
            await emailSender.SendAsync(message);
            return Result.Success("测试邮件发送成功，请检查收件箱");
        }
        catch (Exception ex)
        {
            return Result.Fail(500, $"测试邮件发送失败：{ex.Message}");
        }
    }


}

/// <summary>
/// 测试邮件请求体。
/// </summary>
public sealed class TestEmailInput
{
    /// <summary>
    /// 收件人邮箱地址。
    /// </summary>
    public string? To { get; set; }
}




