using System.Text;
using Ginkgo.Api.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace Ginkgo.Api.Bootstrap;

/// <summary>
/// JWT 认证与授权策略注册（从 Program.cs 提取）。
/// </summary>
public static class AuthenticationSetup
{
    /// <summary>
    /// 注册 JWT 认证 + Authorization 策略。
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        var jwt = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

        // 🔒 安全加固：校验 JWT SigningKey 最小长度
        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
        {
            var msg = "JWT SigningKey 未配置或长度不足 32 字节，请在 appsettings.json 或 db.json 中设置 Jwt:SigningKey（建议 64 字符随机字符串）";
            Log.Fatal("[BOOT] {Error}", msg);
            throw new InvalidOperationException(msg);
        }
        Log.Information("[BOOT] JWT SigningKey 已加载 (长度: {KeyLength} bytes)", Encoding.UTF8.GetByteCount(jwt.SigningKey));

        var key = Encoding.UTF8.GetBytes(jwt.SigningKey);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ClockSkew = TimeSpan.Zero
                };

                // SignalR / 文件端点 / 模块包下载：从查询参数获取 token
                // 其中模块包下载 /api/v1/modules/package 是给 WPF 客户端拉插件 zip 用，
                // WPF 在请求 URL 上附加 access_token 完成 JWT 鉴权（P0-2 安全修复，原来该端点是 [AllowAnonymous]）。
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            (path.StartsWithSegments("/hubs") ||
                             path.StartsWithSegments("/api/v1/files") ||
                             path.StartsWithSegments("/api/v1/modules/package")))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });
        return services;
    }

    /// <summary>
    /// 注册权限策略（仅在非安装模式下）。
    /// </summary>
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        // 权限缓存（IMemoryCache）
        services.AddMemoryCache();
        services.AddSingleton<PermissionCacheInvalidator>();

        // 多租户上下文
        services.AddScoped<Ginkgo.Domain.Tenant.TenantContext>();
        services.AddScoped<Ginkgo.Domain.Tenant.ITenantContext>(sp => sp.GetRequiredService<Ginkgo.Domain.Tenant.TenantContext>());

        // 模块权限注册器
        services.AddScoped<Ginkgo.Api.Modules.ModulePermissionRegistrar>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy("Permission", policy => policy.Requirements.Add(new PermissionRequirement()));

            // 全局兜底策略：任何未显式声明 [Authorize] 或 [AllowAnonymous] 的端点都至少要求登录。
            // 防御目标：未来新增 Controller 漏写 [Authorize] 时，避免无鉴权暴露敏感接口。
            // 仍需精细授权的接口必须显式声明 [Authorize(Policy = "Permission")]，无授权直接放行的接口必须显式 [AllowAnonymous]。
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, FriendlyAuthorizationResultHandler>();
        return services;
    }
}
