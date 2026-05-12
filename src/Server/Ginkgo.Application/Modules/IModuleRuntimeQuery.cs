namespace Ginkgo.Application.Modules;

/// <summary>
/// 运行时模块查询端口（Application 层接口，避免直接依赖 API 层具体实现）。
/// </summary>
public interface IModuleRuntimeQuery
{
    /// <summary>
    /// 判断模块在当前进程中是否已加载（依据运行时管理器）。
    /// </summary>
    bool IsLoaded(string moduleId);
}

