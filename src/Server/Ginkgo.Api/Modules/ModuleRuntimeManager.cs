using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

using Ginkgo.Plugin.Abstractions;
using Ginkgo.Domain;
using Ginkgo.Domain.Modules;

namespace Ginkgo.Api.Modules;

public sealed class ModuleRuntimeManager
{
    private sealed record KnownModule(
        string ModuleId,
        object Instance,
        AssemblyIsolatedLoadContext LoadContext,
        string BaseDirectory,
        ModuleManifest Manifest,
        Assembly Assembly,
        bool Loaded
    );

    private readonly ConcurrentDictionary<string, KnownModule> _known = new(StringComparer.OrdinalIgnoreCase);
    private IServiceProvider? _services;

    private void FireOpLog(string moduleId, string action, string level, string? message = null, object? details = null)
    {
        try
        {
            var repoObj = _services?.GetService(typeof(IRepository<ModuleOpLogEntity>));
            if (repoObj is IRepository<ModuleOpLogEntity> repo)
            {
                var log = new ModuleOpLogEntity
                {
                    Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId(),
                    ModuleId = moduleId,
                    Action = action,
                    Level = level,
                    CreatedAtUtc = DateTime.Now,
                    Message = message,
                    DetailsJson = details is null ? null : JsonSerializer.Serialize(details)
                };
                _ = repo.AddAsync(log);
            }
        }
        catch { }
    }

    // 注册“已知模块”（预加载或动态安装后）。仅登记，不自动加入 MVC 或调用 OnLoad。
    public void RegisterKnown(object instance, AssemblyIsolatedLoadContext alc, string baseDirectory, ModuleManifest manifest, Assembly assembly)
    {
        var km = new KnownModule(manifest.Id, instance, alc, baseDirectory, manifest, assembly, Loaded: false);
        _known[manifest.Id] = km;
    }

    // 从磁盘加载（用于首次启用或热加载失败后的重建）。
    public bool TryCreateAndRegisterFromPath(string moduleId, string entryAssemblyPath, out string? error)
    {
        error = null;
        try
        {
            FireOpLog(moduleId, "Load.Init", "INFO", $"Try create from {entryAssemblyPath}");
            var alc = new AssemblyIsolatedLoadContext($"rt_{moduleId}_{Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId()}", Path.GetDirectoryName(entryAssemblyPath)!);
            var asm = alc.LoadFromAssemblyPath(entryAssemblyPath);
            var moduleType = asm.GetTypes().FirstOrDefault(t => !t.IsInterface && !t.IsAbstract && t.GetInterfaces().Any(i => i.FullName == "Ginkgo.Plugin.Abstractions.IServerModule"));
            if (moduleType == null) { error = "未找到 IServerModule 实现"; FireOpLog(moduleId, "Load.Init", "ERROR", error); return false; }
            var instance = Activator.CreateInstance(moduleType)!;
            // 不在此调用 Initialize（运行时已构建 DI）。模块应在 OnLoadAsync 中完成切换/初始化。
            var manifest = new ModuleManifest { Id = moduleId, Name = moduleId, Version = asm.GetName().Version?.ToString() ?? "0.0.0", HasClient = false, Server = new ServerConfig { EntryAssembly = entryAssemblyPath } };
            RegisterKnown(instance, alc, Path.GetDirectoryName(entryAssemblyPath)!, manifest, asm);
            FireOpLog(moduleId, "Load.Init", "INFO", "Registered known module");
            return true;
        }
        catch (Exception ex) { error = ex.Message; FireOpLog(moduleId, "Load.Init", "ERROR", ex.Message); return false; }
    }

    // 将模块加入 MVC 并触发 OnLoad（热启用）。
    public bool TryLoad(string moduleId, ApplicationPartManager partManager, MvcActionDescriptorChangeProvider changeProvider, IServiceProvider services, out string? error)
    {
        error = null;
        if (!_known.TryGetValue(moduleId, out var km)) { error = "模块未注册/未安装"; FireOpLog(moduleId, "Load.Attempt", "ERROR", error); return false; }
        if (km.Loaded) { FireOpLog(moduleId, "Load.Attempt", "INFO", "Already loaded"); return true; }
        try
        {
            FireOpLog(moduleId, "Load.Attempt", "INFO", "Registering controllers");
            // 注册控制器并通知刷新
            try { partManager.ApplicationParts.Add(new AssemblyPart(km.Assembly)); FireOpLog(moduleId, "Inject.Success", "INFO", "Controllers added"); } catch (Exception ex) { FireOpLog(moduleId, "Inject.Fail", "ERROR", ex.Message); }
            try { changeProvider.NotifyChanges(); } catch { }

            // 调用 OnLoadAsync
            try
            {
                var onLoad = km.Instance.GetType().GetMethod("OnLoadAsync");
                if (onLoad != null)
                {
                    var result = onLoad.Invoke(km.Instance, new object[] { services });
                    if (result is Task task) task.GetAwaiter().GetResult();
                    FireOpLog(moduleId, "Load.Success", "INFO", "OnLoadAsync completed");
                }
                else
                {
                    FireOpLog(moduleId, "Load.Success", "INFO", "No OnLoadAsync method");
                }
            }
            catch (Exception ex)
            {
                FireOpLog(moduleId, "Load.Error", "ERROR", ex.Message);
            }

            // 扫描模块中的 IScheduledTask 实现并注册到调度引擎
            try
            {
                var registryObj = services.GetService(typeof(Ginkgo.Infrastructure.Scheduling.ScheduledTaskRegistry));
                if (registryObj is Ginkgo.Infrastructure.Scheduling.ScheduledTaskRegistry registry)
                {
                    var moduleDisplayName = !string.IsNullOrWhiteSpace(km.Manifest.Title)
                        ? km.Manifest.Title
                        : (!string.IsNullOrWhiteSpace(km.Manifest.Name) ? km.Manifest.Name : moduleId);
                    var taskInterface = typeof(Ginkgo.Plugin.Abstractions.IScheduledTask);
                    var taskTypes = km.Assembly.GetTypes()
                        .Where(t => !t.IsInterface && !t.IsAbstract && taskInterface.IsAssignableFrom(t))
                        .ToList();
                    foreach (var taskType in taskTypes)
                    {
                        try
                        {
                            if (Activator.CreateInstance(taskType) is Ginkgo.Plugin.Abstractions.IScheduledTask taskInstance)
                            {
                                registry.Register(taskInstance, moduleId, moduleDisplayName, moduleDisplayName);
                                FireOpLog(moduleId, "Task.Register", "INFO", $"已注册定时任务: {taskInstance.TaskKey}");
                            }
                        }
                        catch (Exception tex) { FireOpLog(moduleId, "Task.Register", "ERROR", $"注册任务 {taskType.Name} 失败: {tex.Message}"); }
                    }
                }
            }
            catch (Exception ex) { FireOpLog(moduleId, "Task.Scan", "ERROR", ex.Message); }

            // 扫描模块中的 IInvocableAction 实现并注册到 ActionRegistry
            try
            {
                var actionRegistryObj = services.GetService(typeof(Ginkgo.Infrastructure.Scheduling.ActionRegistry));
                if (actionRegistryObj is Ginkgo.Infrastructure.Scheduling.ActionRegistry actionRegistry)
                {
                    var moduleDisplayName = !string.IsNullOrWhiteSpace(km.Manifest.Title)
                        ? km.Manifest.Title
                        : (!string.IsNullOrWhiteSpace(km.Manifest.Name) ? km.Manifest.Name : moduleId);

                    // 桥接模块的 IScheduledTask 为 IInvocableAction
                    var taskRegistryObj = services.GetService(typeof(Ginkgo.Infrastructure.Scheduling.ScheduledTaskRegistry));
                    if (taskRegistryObj is Ginkgo.Infrastructure.Scheduling.ScheduledTaskRegistry taskReg)
                    {
                        foreach (var reg in taskReg.GetAll().Where(r => string.Equals(r.Source, moduleId, StringComparison.OrdinalIgnoreCase)))
                        {
                            var bridge = new Ginkgo.Infrastructure.Scheduling.ScheduledTaskActionBridge(reg.Task);
                            actionRegistry.Register(bridge, moduleId, moduleDisplayName);
                        }
                    }

                    // 扫描模块中直接实现的 IInvocableAction
                    var actionInterface = typeof(Ginkgo.Plugin.Abstractions.IInvocableAction);
                    var actionTypes = km.Assembly.GetTypes()
                        .Where(t => !t.IsInterface && !t.IsAbstract && actionInterface.IsAssignableFrom(t))
                        .ToList();
                    foreach (var actionType in actionTypes)
                    {
                        try
                        {
                            if (Activator.CreateInstance(actionType) is Ginkgo.Plugin.Abstractions.IInvocableAction actionInstance)
                            {
                                actionRegistry.Register(actionInstance, moduleId, moduleDisplayName);
                                FireOpLog(moduleId, "Action.Register", "INFO", $"已注册可调用动作: {actionInstance.ActionKey}");
                            }
                        }
                        catch (Exception aex) { FireOpLog(moduleId, "Action.Register", "ERROR", $"注册动作 {actionType.Name} 失败: {aex.Message}"); }
                    }
                }
            }
            catch (Exception ex) { FireOpLog(moduleId, "Action.Scan", "ERROR", ex.Message); }

            // 扫描模块中的 ITaskExecutionProvider 实现并注册到 ExecutionProviderRegistry
            try
            {
                var providerRegistryObj = services.GetService(typeof(Ginkgo.Infrastructure.Scheduling.ExecutionProviderRegistry));
                if (providerRegistryObj is Ginkgo.Infrastructure.Scheduling.ExecutionProviderRegistry providerRegistry)
                {
                    var providerInterface = typeof(Ginkgo.Plugin.Abstractions.ITaskExecutionProvider);
                    var providerTypes = km.Assembly.GetTypes()
                        .Where(t => !t.IsInterface && !t.IsAbstract && providerInterface.IsAssignableFrom(t))
                        .ToList();
                    foreach (var providerType in providerTypes)
                    {
                        try
                        {
                            if (Activator.CreateInstance(providerType) is Ginkgo.Plugin.Abstractions.ITaskExecutionProvider providerInstance)
                            {
                                providerRegistry.Register(providerInstance, moduleId);
                                FireOpLog(moduleId, "Provider.Register", "INFO", $"已注册执行提供器: {providerInstance.SourceKey}");
                            }
                        }
                        catch (Exception pex) { FireOpLog(moduleId, "Provider.Register", "ERROR", $"注册提供器 {providerType.Name} 失败: {pex.Message}"); }
                    }
                }
            }
            catch (Exception ex) { FireOpLog(moduleId, "Provider.Scan", "ERROR", ex.Message); }

            // 标记为已加载
            _known[moduleId] = km with { Loaded = true };
            return true;
        }
        catch (Exception ex) { error = ex.Message; FireOpLog(moduleId, "Load.Error", "ERROR", ex.Message); return false; }
    }

    // 从 MVC 移除并卸载 ALC（热禁用）。
    public bool TryUnload(string moduleId, ApplicationPartManager partManager, MvcActionDescriptorChangeProvider changeProvider, out string? error)
    {
        error = null;
        if (!_known.TryGetValue(moduleId, out var km)) { error = "模块未注册/未安装"; FireOpLog(moduleId, "Unload.Attempt", "ERROR", error); return false; }
        try
        {
            FireOpLog(moduleId, "Unload.Attempt", "INFO", "Calling OnUnloadAsync");
            // OnUnload 清理与平滑退出 (Graceful Unload)
            try
            {
                if (km.Instance is IGracefulUnloadable graceful)
                {
                    FireOpLog(moduleId, "Unload.Graceful", "INFO", "Calling IGracefulUnloadable.StopAsync");
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    try { graceful.StopAsync(cts.Token).GetAwaiter().GetResult(); } catch (Exception ex) { FireOpLog(moduleId, "Unload.GracefulError", "ERROR", ex.Message); }
                }

                var onUnload = km.Instance.GetType().GetMethod("OnUnloadAsync");
                if (onUnload != null)
                {
                    var result = onUnload.Invoke(km.Instance, Array.Empty<object>());
                    if (result is Task task) task.GetAwaiter().GetResult();
                }
            }
            catch (Exception ex) { FireOpLog(moduleId, "Unload.Error", "ERROR", ex.Message); }

            // 注销模块的定时任务
            try
            {
                var registryObj = _services?.GetService(typeof(Ginkgo.Infrastructure.Scheduling.ScheduledTaskRegistry));
                if (registryObj is Ginkgo.Infrastructure.Scheduling.ScheduledTaskRegistry registry)
                {
                    registry.UnregisterBySource(moduleId);
                    FireOpLog(moduleId, "Task.Unregister", "INFO", "已注销该模块的所有定时任务");
                }
            }
            catch (Exception ex) { FireOpLog(moduleId, "Task.Unregister", "ERROR", ex.Message); }

            // 注销模块的可调用动作
            try
            {
                var actionRegistryObj = _services?.GetService(typeof(Ginkgo.Infrastructure.Scheduling.ActionRegistry));
                if (actionRegistryObj is Ginkgo.Infrastructure.Scheduling.ActionRegistry actionRegistry)
                {
                    actionRegistry.UnregisterBySource(moduleId);
                    FireOpLog(moduleId, "Action.Unregister", "INFO", "已注销该模块的所有可调用动作");
                }
            }
            catch (Exception ex) { FireOpLog(moduleId, "Action.Unregister", "ERROR", ex.Message); }

            // 注销模块的执行提供器
            try
            {
                var providerRegistryObj = _services?.GetService(typeof(Ginkgo.Infrastructure.Scheduling.ExecutionProviderRegistry));
                if (providerRegistryObj is Ginkgo.Infrastructure.Scheduling.ExecutionProviderRegistry providerRegistry)
                {
                    providerRegistry.UnregisterBySource(moduleId);
                    FireOpLog(moduleId, "Provider.Unregister", "INFO", "已注销该模块的所有执行提供器");
                }
            }
            catch (Exception ex) { FireOpLog(moduleId, "Provider.Unregister", "ERROR", ex.Message); }

            // 从 MVC 路由移除
            try
            {
                var toRemove = partManager.ApplicationParts.OfType<AssemblyPart>().Where(p => ReferenceEquals(p.Assembly, km.Assembly) || string.Equals(p.Assembly.GetName().Name, km.Assembly.GetName().Name, StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var ap in toRemove) partManager.ApplicationParts.Remove(ap);
            }
            catch { }
            try { changeProvider.NotifyChanges(); } catch { }

            // 卸载 ALC
            try { km.LoadContext.Unload(); } catch { }

            // 从已知表中移除（完全释放；重新启用将重新加载）
            _known.TryRemove(moduleId, out _);
            FireOpLog(moduleId, "Unload.Success", "INFO", "ALC unloaded and removed");
            return true;
        }
        catch (Exception ex) { error = ex.Message; FireOpLog(moduleId, "Unload.Error", "ERROR", ex.Message); return false; }
    }

    public async Task OnAppStartedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        _services = services;
        // 将已标记为 Loaded 的模块执行 OnLoadAsync（如果在 Build 前已加入 MVC）。
        foreach (var kv in _known.Values.Where(v => v.Loaded))
        {
            try
            {
                var onLoadMethod = kv.Instance.GetType().GetMethod("OnLoadAsync");
                if (onLoadMethod != null)
                {
                    var result = onLoadMethod.Invoke(kv.Instance, new object[] { services });
                    if (result is Task task) await task;
                }
            }
            catch { }
        }
    }

    // 运行时查询：判断指定模块是否已加载
    public bool IsLoaded(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId)) return false;
        return _known.TryGetValue(moduleId, out var km) && km.Loaded;
    }
}

