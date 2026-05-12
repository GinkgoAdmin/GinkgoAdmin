using Ginkgo.Application.Modules;

namespace Ginkgo.Api.Modules;

/// <summary>
/// IModuleRuntimeQuery 适配器，将 ModuleRuntimeManager 的 IsLoaded 方法桥接到 IModuleRuntimeQuery 接口。
/// </summary>
public sealed class ModuleRuntimeQueryAdapter : IModuleRuntimeQuery
{
    private readonly ModuleRuntimeManager _runtime;
    public ModuleRuntimeQueryAdapter(ModuleRuntimeManager runtime) => _runtime = runtime;
    public bool IsLoaded(string moduleId) => _runtime.IsLoaded(moduleId);
}

