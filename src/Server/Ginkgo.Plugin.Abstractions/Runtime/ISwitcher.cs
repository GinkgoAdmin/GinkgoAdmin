namespace Ginkgo.Infrastructure.Runtime;

/// <summary>
/// 运行时可切换服务的抽象接口。
/// 用于在不重启应用的情况下动态替换某个服务的实现（如文件存储提供者）。
/// </summary>
public interface ISwitcher<T> where T : class
{
	/// <summary>当前生效的服务实例。</summary>
	T Current { get; }

	/// <summary>将服务切换为指定实例。</summary>
	void SwitchTo(T instance);
}
