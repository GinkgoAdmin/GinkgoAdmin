namespace Ginkgo.Plugin.Abstractions;

/// <summary>
/// 长驻服务平滑卸载接口。模块如果启动了后台任务或长连接，可实现此接口以在模块被卸载前接收信号进行资源清理和平滑退出。
/// </summary>
public interface IGracefulUnloadable
{
    /// <summary>
    /// 在模块即将被卸载时触发。
    /// </summary>
    /// <param name="cancellationToken">超时取消令牌，通常由宿主提供一个安全停止的最大等待时间。</param>
    Task StopAsync(CancellationToken cancellationToken);
}
