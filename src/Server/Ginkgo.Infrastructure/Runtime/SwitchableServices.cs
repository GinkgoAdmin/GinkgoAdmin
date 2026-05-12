using System.Reflection;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Ginkgo.Infrastructure.Runtime;

internal sealed class SwitchHolder<T> where T : class
{
	public T Current;
	public SwitchHolder(T initial) { Current = initial; }
}

public class SwitchProxy<T> : DispatchProxy where T : class
{
	internal SwitchHolder<T> Holder = null!;
	protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
		=> targetMethod!.Invoke(Holder.Current, args);
}

internal sealed class DefaultSwitcher<T> : ISwitcher<T> where T : class
{
	private readonly SwitchHolder<T> _holder;
	public DefaultSwitcher(SwitchHolder<T> holder) { _holder = holder; }
	public T Current => _holder.Current;
	public void SwitchTo(T instance) => Interlocked.Exchange(ref _holder.Current, instance);
}

public static class ServiceCollectionSwitchableExtensions
{
	public static IServiceCollection AddSwitchable<T>(this IServiceCollection services, Func<IServiceProvider, T> initialFactory)
		where T : class
	{
		services.AddSingleton<SwitchHolder<T>>(sp => new SwitchHolder<T>(initialFactory(sp)));
		services.AddSingleton<T>(sp =>
		{
			var holder = sp.GetRequiredService<SwitchHolder<T>>();
			var proxy = DispatchProxy.Create<T, SwitchProxy<T>>();
			((SwitchProxy<T>)(object)proxy!).Holder = holder;
			return proxy!;
		});
		services.AddSingleton<ISwitcher<T>>(sp => new DefaultSwitcher<T>(sp.GetRequiredService<SwitchHolder<T>>()));
		return services;
	}
}


