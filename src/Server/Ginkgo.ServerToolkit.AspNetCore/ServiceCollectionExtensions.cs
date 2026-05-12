using Microsoft.Extensions.DependencyInjection;

namespace Ginkgo.ServerToolkit.AspNetCore;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddServerToolkitAspNetCore(this IServiceCollection services)
	{
		// 预留：添加异常中间件、模型绑定器、速率限制等 ASP.NET 适配内容
		return services;
	}
}






