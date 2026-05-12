using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.RateLimiting;
using Ginkgo.Api.Filters;
using Ginkgo.Api.Modules;
using Ginkgo.Infrastructure.Persistence.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Ginkgo.Infrastructure.Persistence; // DataScopeOptions

namespace Ginkgo.Api.Bootstrap;

/// <summary>
/// API 层服务注册入口（分方法解耦 Program.cs）。
/// 先从持久化注册开始，后续逐步迁移 ApiCore/Application/Modules 注册。
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// 注册持久化组件（EF 上下文 + SqlSugar 客户端与仓储）。
    /// 读取 Database:Provider/ConnectionStrings:Default 等配置。
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        // 复用现有实现，保持行为不变
        services.AddGinkgoDbByConfiguration(configuration);
        // 注册 UoW（SqlSugar 实现）
        services.AddScoped<Ginkgo.Domain.IUnitOfWork, Ginkgo.Infrastructure.Persistence.SqlSugar.SqlSugarUnitOfWork>();
        return services;
    }

    /// <summary>
    /// 预留：注册 API 核心（Controllers/Swagger/Cors 等）。
    /// 当前先留空，后续阶段逐步迁移。
    /// </summary>
    public static IServiceCollection AddApiCore(this IServiceCollection services, IConfiguration configuration)
    {
        // 🔒 API 速率限制（防暴力破解 / DDoS）
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/json";
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var ra) ? ra : TimeSpan.FromSeconds(60);
                context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                await context.HttpContext.Response.WriteAsJsonAsync(new { code = 429, message = "请求过于频繁，请稍后再试" }, cancellationToken);
            };

            // 登录接口：每 IP 每分钟 5 次
            options.AddPolicy("login", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));

            // 安装接口：每 IP 每分钟 10 次
            options.AddPolicy("install", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));

            // 上传接口：每 IP 每分钟 20 次
            options.AddPolicy("upload", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));

            // P2：模块包下载接口（GetPackage）。WPF 客户端启动会按 ModuleManifest 顺序拉取多个包，
            // 上限放宽到 60 次/分钟/IP，足够正常启动的同时阻止脚本化扫库式抓取。
            options.AddPolicy("download", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));

            // P2：模块运维操作（启用/禁用、热重载、重置菜单等）。比 install 更频繁，但仍是写操作，
            // 60 次/分钟/IP 的窗口足以覆盖运维批量操作，又能限制扫描/暴力调用。
            options.AddPolicy("module-ops", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));

            // P2：支付状态主动查询（PluginStore 商城的「我已完成支付」按钮 + 周期兜底）。
            // 该接口最终会触发远端 Payment 模块向第三方支付网关发起一次 Query，必须设上限以
            // 防止恶意客户端高频打微信/支付宝接口（导致网关风控或商户限流）。
            // 60 次/分钟/IP 足以覆盖：用户多次手动点击 + 多标签页同时打开 + 周期性兜底轮询。
            options.AddPolicy("payment-check", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
        });

        // 响应压缩（Gzip + Brotli），显著减少 JSON 响应传输时间
        // 注意：需要排除 text/event-stream，避免 SSE 流被缓冲导致实时推送失效
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes
                .Concat(new[] { "application/json" });
            // 排除 SSE 流的 MIME 类型，避免响应压缩缓冲导致 FlushAsync 无法实时推送数据
            options.ExcludedMimeTypes = new[] { "text/event-stream" };
        });

        // Controllers + Mvc 部分（与 Program.cs 行为一致）
        var mvcBuilder = services.AddControllers()
            .AddJsonOptions(options =>
            {
                // 使用 camelCase 命名策略，与前端约定一致
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                // 将 long 类型序列化为字符串，避免 JavaScript 精度丢失
                options.JsonSerializerOptions.Converters.Add(new LongToStringConverter());
                options.JsonSerializerOptions.Converters.Add(new NullableLongToStringConverter());
                // DateTime UTC 序列化：MySQL 读回的 DateTime.Kind = Unspecified，不带 Z 后缀
                // 导致前端浏览器误以为是本地时间（实际是 UTC），产生 8 小时偏差
                // 强制所有 DateTime 以 UTC 格式（带 Z）输出，浏览器自动转为本地时间
                options.JsonSerializerOptions.Converters.Add(new DateTimeToUtcConverter());
                options.JsonSerializerOptions.Converters.Add(new NullableDateTimeToUtcConverter());
            });
        services.AddSingleton(mvcBuilder.PartManager);
        services.AddSingleton<IActionDescriptorChangeProvider>(MvcActionDescriptorChangeProvider.Instance);
        services.AddSingleton(MvcActionDescriptorChangeProvider.Instance);
        services.AddHttpContextAccessor();

        // 接口注释（标题）目录（横切能力）：扫描所有 MVC Action 上的 [EndpointComment]，供运维/监控反查
        services.AddSingleton<Ginkgo.Api.Services.EndpointDescriptionCatalog>();

        // ASP.NET Core 标准健康检查
        services.AddHealthChecks();

        // Swagger + Endpoints 发现
        services.AddEndpointsApiExplorer();

        // 读取配置的需要并入主文档的分组（可选）
        var includeGroupsArray = configuration.GetSection("Swagger:IncludeGroups").Get<string[]>() ?? Array.Empty<string>();
        var includeGroups = new HashSet<string>(includeGroupsArray, StringComparer.OrdinalIgnoreCase);
        services.Configure<SwaggerGroupOptions>(opts =>
        {
            opts.IncludeGroups = new HashSet<string>(includeGroups, StringComparer.OrdinalIgnoreCase);
        });

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new() { Title = "Ginkgo API", Version = "v1", Description = "敏捷框架 API" });

            // 仅在命中配置的 GroupName 时，使用其 GroupName 作为 Tag；其余保持原样
            options.OperationFilter<CommunityGroupTagOperationFilter>();

            // 将配置命中的分组并入 v1 文档；其余保持原有（v1 或未分组）
            options.DocInclusionPredicate((docName, apiDesc) =>
            {
                if (!string.Equals(docName, "v1", StringComparison.OrdinalIgnoreCase)) return false;
                var group = apiDesc.GroupName;
                return string.IsNullOrEmpty(group)
                    || string.Equals(group, docName, StringComparison.OrdinalIgnoreCase)
                    || (group != null && includeGroups.Contains(group));
            });


            // 自定义 SchemaId（保持与 Program.cs 一致）
            var schemaIdCounter = new Dictionary<string, int>();
            options.CustomSchemaIds(type =>
            {
                var baseId = type.FullName?.Replace("+", ".") ?? type.Name;
                if (schemaIdCounter.ContainsKey(baseId))
                {
                    schemaIdCounter[baseId]++;
                    return $"{baseId}_{schemaIdCounter[baseId]}";
                }
                else
                {
                    schemaIdCounter[baseId] = 0;
                    return baseId;
                }
            });

            // XML 注释
            var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml", SearchOption.AllDirectories);
            foreach (var xml in xmlFiles)
            {
                try { options.IncludeXmlComments(xml, includeControllerXmlComments: true); } catch { }
            }

            // 安全定义
            options.AddSecurityDefinition("Bearer", new()
            {
                Description = "在下方输入 Bearer {token}",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });
            options.AddSecurityRequirement(new()
            {
                {
                    new() { Reference = new() { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                    new string[] {}
                }
            });
        });

        // CORS 策略（支持从 appsettings 与数据库 Site.Cors.AllowedOrigins 合并读取）
        static string[] ParseOrigins(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return Array.Empty<string>();
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        // 读取 appsettings 的 Cors:AllowedOrigins
        var cfgCsv = configuration["Cors:AllowedOrigins"] ?? string.Empty;

        // 读取数据库 Site.Cors.AllowedOrigins（一次性，启动时合并）
        string dbCsv = string.Empty;
        try
        {
            using var sp = services.BuildServiceProvider();
            var repo = sp.GetService<Ginkgo.Domain.Settings.ISettingsRepository>();
            var setting = repo != null ? repo.GetAsync("Site.Cors.AllowedOrigins", null).GetAwaiter().GetResult() : null;
            dbCsv = setting?.Value ?? string.Empty;
        }
        catch { }

        var rules = ParseOrigins(cfgCsv).Concat(ParseOrigins(dbCsv)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        // 控制台输出已生效的 CORS 规则，便于调试
        try { Console.WriteLine($"[CORS] Effective AllowedOrigins ({rules.Length}): {string.Join(", ", rules)}"); } catch { }

        bool MatchRule(string origin, string rule)
        {
            if (string.IsNullOrWhiteSpace(rule)) return false;
            if (string.Equals(rule, "*", StringComparison.Ordinal)) return true;
            try
            {
                var uri = new Uri(origin);
                var host = uri.Host;
                var port = uri.IsDefaultPort ? (string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? 443 : 80) : uri.Port;

                if (rule.StartsWith("*.", StringComparison.Ordinal))
                {
                    var suffix = rule.Substring(1); // like .example.com
                    return host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
                }

                if (rule.Contains("://", StringComparison.Ordinal))
                {
                    // 完整 origin（含协议）精确匹配
                    return string.Equals(origin, rule, StringComparison.OrdinalIgnoreCase);
                }

                if (rule.Contains(':'))
                {
                    // host:port 形式
                    return string.Equals($"{host}:{port}", rule, StringComparison.OrdinalIgnoreCase);
                }

                // 仅 host 形式
                return string.Equals(host, rule, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        services.AddCors(options =>
        {
            options.AddPolicy("ConfiguredCors", policy =>
            {
                if (rules.Length == 0)
                {
                    // 🔒 安全修复：默认不开放跨域，仅允许本地开发环境访问
                    // 生产环境必须在 appsettings 或数据库 Site.Cors.AllowedOrigins 中配置允许的域名
                    Console.WriteLine("[CORS] ⚠️ WARNING: No AllowedOrigins configured! Only localhost origins are allowed.");
                    Console.WriteLine("[CORS] To allow cross-origin requests, configure 'Cors:AllowedOrigins' in appsettings or 'Site.Cors.AllowedOrigins' in database settings.");
                    policy.SetIsOriginAllowed(origin =>
                    {
                        try
                        {
                            var uri = new Uri(origin);
                            return uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host == "::1";
                        }
                        catch { return false; }
                    })
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
                }
                else
                {
                    policy.SetIsOriginAllowed(origin => rules.Any(rule => MatchRule(origin, rule)))
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                }
            });
        });

        // MVC 过滤器
        services.Configure<MvcOptions>(options =>
        {
            options.Filters.Add<ValidateModelAttribute>();
        });

        // API 版本化
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });
        services.AddVersionedApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        // 绑定数据范围配置（DataScope）—— 默认 Enabled=false，确保兼容
        services.Configure<DataScopeOptions>(configuration.GetSection("DataScope"));

        return services;
    }

    /// <summary>
    /// 预留：注册应用层服务（命令/查询处理器、DTO 等）。
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // 应用层服务注册（按需逐步迁移）
        services.AddScoped<Ginkgo.Application.Logs.ILogAppService, Ginkgo.Application.Logs.LogAppService>();
        services.AddScoped<Ginkgo.Application.Modules.IModuleAppService, Ginkgo.Application.Modules.ModuleAppService>();

        // 插件消息服务（供模块发送系统消息）
        services.AddScoped<Ginkgo.Plugin.Abstractions.IPluginMessageService, Ginkgo.Application.Messages.PluginMessageService>();

        // 基础设施：邮件发送（默认日志实现，可替换为实际 SMTP/第三方）
        services.AddScoped<Ginkgo.ServerToolkit.IEmailSender, Ginkgo.Infrastructure.Email.SystemSettingsSmtpEmailSender>();

        // 验证码仓储（主框架基础能力）
        services.AddScoped<Ginkgo.Domain.Verification.IVerificationCodeRepository,
            Ginkgo.Infrastructure.Persistence.SqlSugar.Verification.VerificationCodeRepository>();
        services.AddScoped<Ginkgo.Domain.Verification.IVerificationTemplateRepository,
            Ginkgo.Infrastructure.Persistence.SqlSugar.Verification.VerificationTemplateRepository>();

        // 领域事件总线与订阅者（阶段四）
        services.AddSingleton<Ginkgo.Domain.Events.IDomainEventPublisher, Ginkgo.Infrastructure.Messaging.InMemoryDomainEventBus>();
        services.AddScoped<Ginkgo.Domain.Events.IDomainEventHandler<Ginkgo.Domain.Logs.Events.OpLogAppended>, Ginkgo.Application.Logs.Handlers.OpLogAppendedHandler>();
        // 用户相关事件订阅（登录/登出/注册）
        services.AddScoped<Ginkgo.Domain.Events.IDomainEventHandler<Ginkgo.Domain.Users.Events.UserLoggedIn>, Ginkgo.Application.Logs.Handlers.UserLoggedInHandler>();
        services.AddScoped<Ginkgo.Domain.Events.IDomainEventHandler<Ginkgo.Domain.Users.Events.UserLoggedOut>, Ginkgo.Application.Logs.Handlers.UserLoggedOutHandler>();
        // 用户注册完成事件订阅（默认角色/部门）
        services.AddScoped<Ginkgo.Domain.Events.IDomainEventHandler<Ginkgo.Domain.Users.Events.UserRegistered>, Ginkgo.Application.Users.Handlers.UserRegisteredHandler>();
        return services;
    }

    /// <summary>
    /// 预留：注册模块相关服务。
    /// </summary>
    public static IServiceCollection AddModules(this IServiceCollection services)
    {
        // 模块运行时查询适配器（Application 端口 -> API 实现）
        services.AddSingleton<Ginkgo.Application.Modules.IModuleRuntimeQuery, Ginkgo.Api.Modules.ModuleRuntimeQueryAdapter>();
        // 模块安装端口适配器
        services.AddScoped<Ginkgo.Application.Modules.IModuleInstallerPort, Ginkgo.Api.Modules.ModuleInstallerPortAdapter>();
        return services;
    }
}
