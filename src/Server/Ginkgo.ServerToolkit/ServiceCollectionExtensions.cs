using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ginkgo.ServerToolkit;

public static class ServiceCollectionExtensions
{
	/// <summary>
	/// 注册 ServerToolkit 核心能力（默认实现）。
	/// </summary>
	public static IServiceCollection AddServerToolkit(this IServiceCollection services, IConfiguration configuration)
	{
		// HttpContext 访问
		services.AddHttpContextAccessor();
		// 内存缓存（验证码默认实现）
		services.AddMemoryCache();
		// 核心实现
		services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
		services.AddScoped<IServerNotifier, ServerNotifierAdapter>();
		services.AddSingleton<IVerificationCodeService, VerificationCodeService>();
		// 从配置读取站点名称，传入验证服务用于邮件模板 {appName} 占位符
		var appName = configuration.GetValue<string>("Site:Name") ?? configuration.GetValue<string>("AppName") ?? "GinkgoAdmin";
		services.AddScoped<ISecondaryVerificationService>(sp =>
		{
			return new SecondaryVerificationService(
				sp.GetRequiredService<IVerificationCodeService>(),
				sp.GetRequiredService<IEmailSender>(),
				sp.GetServices<IVerificationChannelProvider>(),
				sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
				sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SecondaryVerificationService>>(),
				sp.GetService<Ginkgo.Domain.Verification.IVerificationCodeRepository>(),
				sp.GetService<Ginkgo.Domain.Verification.IVerificationTemplateRepository>(),
				appName);
		});
		// 默认为 NoopEmailSender，宿主可通过 Provider 覆盖（如 SMTP）
		services.TryAddScoped<IEmailSender, NoopEmailSender>();
		// 内置邮件验证码渠道（主框架基础能力）
		services.AddScoped<IVerificationChannelProvider, EmailVerificationChannelProvider>();
		services.AddScoped<IServerToolkit, ServerToolkitFacade>();
		return services;
	}

	/// <summary>
	/// 注册 ServerToolkit ASP.NET Core 扩展（过滤器、中间件等 Web 层适配）。
	/// </summary>
	public static IServiceCollection AddServerToolkitAspNetCore(this IServiceCollection services)
	{
		// 预留：添加异常中间件、模型绑定器、速率限制等 ASP.NET 适配内容
		return services;
	}
}


