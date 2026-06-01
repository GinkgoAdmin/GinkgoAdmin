// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）集成测试 —— 成功注入处理日志（Requirement 5.5）。
// 用 mock（捕获式）logger 验证：当安装链路成功处理一条合法 clientType 声明并注入入口项后，
// ModuleSqlExecutor.ApplyClientMenusAsync 产生一条 Information 级别、且内容含 clientType / Module / 数量
// 的处理日志，从而保证安装可见性。
//
// 测试取向：本用例直接驱动主框架真实的 ApplyClientMenusAsync 代码路径（非 mock 业务逻辑），
// 仅对其外部协作者做最小替身：
//   - IDialectRegistry / IConfiguration：满足 ModuleSqlExecutor 构造函数依赖（构造期按 Database:Provider 解析方言）。
//   - ILogger<ModuleSqlExecutor>：捕获式 mock logger，记录每条日志的级别与渲染后文本，供断言。
//   - IMenuGroupAppService：替身服务，GetDefaultGroupIdAsync 返回非空默认组 Id 使注入走「成功路径」，
//     UpsertClientMenuItemsAsync 为空操作（无副作用），从而触发成功注入后的处理日志。
// 测试框架：xUnit（与本测试工程既有用例一致）。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ginkgo.Api.Modules;
using Ginkgo.Application.Menus;
using Ginkgo.Infrastructure.Abstractions;
using Ginkgo.Infrastructure.Dialects;
using Ginkgo.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Ginkgo.Application.Tests.Integration;

/// <summary>
/// 集成：multi-client-plugin-portal —— 成功注入处理日志（Requirement 5.5）。
/// </summary>
public sealed class ClientMenusInjectionLoggingTests
{
    /// <summary>
    /// 一次成功注入应产生一条含 clientType / Module / 数量 的 Information 级处理日志（需求 5.5）。
    /// </summary>
    [Fact]
    public async Task ApplyClientMenusAsync_成功注入后应记录含_clientType_Module_数量_的处理日志()
    {
        // Arrange：构建满足 ModuleSqlExecutor 依赖的测试容器。
        const string moduleId = "Ginkgo.Module.SmartCommunity"; // 区分大小写，模拟 module.json 的 Id
        const long defaultGroupId = 1234567890123456789L;        // 任意非空默认组 Id，使注入走成功路径

        var capturingLogger = new CapturingLogger<ModuleSqlExecutor>();
        var menuGroupStub = new StubMenuGroupAppService(defaultGroupId);

        var services = new ServiceCollection();
        // 构造期依赖：方言注册中心（真实实现 + MySQL 方言）与配置（Database:Provider=mysql）。
        services.AddSingleton<IDialectRegistry>(new DialectRegistry(new IDatabaseDialect[] { new MySqlDialect() }));
        services.AddSingleton<IConfiguration>(new StubConfiguration(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "mysql"
        }));
        // 运行期依赖：捕获式 logger 与替身菜单组服务（在 ApplyClientMenusAsync 内部按 scope 解析）。
        services.AddSingleton<ILogger<ModuleSqlExecutor>>(capturingLogger);
        services.AddSingleton<IMenuGroupAppService>(menuGroupStub);

        using var provider = services.BuildServiceProvider();
        var executor = new ModuleSqlExecutor(provider);

        // 一条合法 UNIAPP 声明，含两个入口项。
        var spec = new ModuleSqlExecutor.InstallSpec
        {
            ModuleId = moduleId,
            ClientMenus = new List<ModuleSqlExecutor.ClientMenusSpec>
            {
                new()
                {
                    ClientType = "UNIAPP",
                    Items = new List<ModuleSqlExecutor.ClientMenuItemSpec>
                    {
                        new() { Title = "事件办理", Icon = "ri-mic-line", Path = "/pages/plugins/smart-community/event-handle", RequireGrant = true, Order = 1 },
                        new() { Title = "智慧社区", Icon = "ri-community-line", Path = "/pages/plugins/smart-community/index", RequireGrant = false, Order = 2 }
                    }
                }
            }
        };

        // Act：执行真实注入代码路径。
        await executor.ApplyClientMenusAsync(spec, moduleId, CancellationToken.None);

        // Assert：替身服务确实被以归一化 clientType 调用，且注入了 2 个入口项（确认走了成功路径）。
        Assert.Equal("UNIAPP", menuGroupStub.LastUpsertClientType);
        Assert.Equal(moduleId, menuGroupStub.LastUpsertModuleId);
        Assert.Equal(2, menuGroupStub.LastUpsertItemCount);

        // 应恰好产生一条 Information 级处理日志（非法/无默认组分支会写 Warning，这里不应出现）。
        var infoLogs = capturingLogger.Logs.Where(l => l.Level == LogLevel.Information).ToList();
        var processingLog = Assert.Single(infoLogs);

        // 处理日志内容必须同时包含 clientType、Module 与注入数量（需求 5.5）。
        Assert.Contains("clientType=UNIAPP", processingLog.Message);
        Assert.Contains($"Module={moduleId}", processingLog.Message);
        Assert.Contains("数量=2", processingLog.Message);

        // 不应出现任何警告日志（合法声明 + 存在默认组）。
        Assert.DoesNotContain(capturingLogger.Logs, l => l.Level == LogLevel.Warning);
    }

    // ============================================================
    // 测试替身：捕获式 ILogger<T>
    // ============================================================

    /// <summary>
    /// 捕获每条日志的级别与渲染后文本的 mock logger，供断言日志内容。
    /// </summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<CapturedLog> Logs { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // formatter 会按结构化模板渲染出最终文本（命名占位符替换为实参），
            // 因此渲染结果会形如「…clientType=UNIAPP, Module=…, 数量=2…」。
            Logs.Add(new CapturedLog(logLevel, formatter(state, exception)));
        }

        private sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new();
            public void Dispose() { }
        }
    }

    /// <summary>
    /// 单条被捕获的日志记录（级别 + 渲染后文本）。
    /// </summary>
    private sealed record CapturedLog(LogLevel Level, string Message);

    // ============================================================
    // 测试替身：IConfiguration（仅供构造期读取 Database:Provider）
    // ============================================================

    /// <summary>
    /// 仅实现键值索引的最小 <see cref="IConfiguration"/> 替身；其余成员未在本测试路径中使用。
    /// </summary>
    private sealed class StubConfiguration : IConfiguration
    {
        private readonly Dictionary<string, string?> _values;

        public StubConfiguration(Dictionary<string, string?> values) => _values = values;

        public string? this[string key]
        {
            get => _values.TryGetValue(key, out var v) ? v : null;
            set => _values[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();

        public IChangeToken GetReloadToken() => NullChangeToken.Instance;

        public IConfigurationSection GetSection(string key)
            => throw new NotSupportedException("测试替身未实现 GetSection（本测试路径不需要）。");

        private sealed class NullChangeToken : IChangeToken
        {
            public static readonly NullChangeToken Instance = new();
            public bool HasChanged => false;
            public bool ActiveChangeCallbacks => false;
            public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => NoopDisposable.Instance;

            private sealed class NoopDisposable : IDisposable
            {
                public static readonly NoopDisposable Instance = new();
                public void Dispose() { }
            }
        }
    }

    // ============================================================
    // 测试替身：IMenuGroupAppService（仅实现注入成功路径所需的两个方法）
    // ============================================================

    /// <summary>
    /// 菜单组应用服务替身：使 <see cref="ApplyClientMenusAsync"/> 走「存在默认组 → 注入成功」路径。
    /// 仅 <see cref="GetDefaultGroupIdAsync"/> 与 <see cref="UpsertClientMenuItemsAsync"/> 有意义，
    /// 其余成员在本测试路径不会被调用，统一抛出 <see cref="NotSupportedException"/>。
    /// </summary>
    private sealed class StubMenuGroupAppService : IMenuGroupAppService
    {
        private readonly long _defaultGroupId;

        public StubMenuGroupAppService(long defaultGroupId) => _defaultGroupId = defaultGroupId;

        // 记录最后一次注入调用的入参，供测试断言成功路径已被执行。
        public string? LastUpsertClientType { get; private set; }
        public string? LastUpsertModuleId { get; private set; }
        public int LastUpsertItemCount { get; private set; }

        public Task<long?> GetDefaultGroupIdAsync(string clientType, CancellationToken ct = default)
            => Task.FromResult<long?>(_defaultGroupId);

        public Task UpsertClientMenuItemsAsync(string clientType, string moduleId, IEnumerable<ClientMenuItemSpec> items, CancellationToken ct = default)
        {
            LastUpsertClientType = clientType;
            LastUpsertModuleId = moduleId;
            LastUpsertItemCount = items?.Count() ?? 0;
            return Task.CompletedTask; // 空操作：本测试只关心成功注入后的处理日志
        }

        // ===== 以下成员在本测试路径不会被触发 =====

        public Task<List<MenuGroupListItemDto>> GetGroupListAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MenuGroupDetailDto?> GetGroupAsync(long id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<long> CreateGroupAsync(CreateMenuGroupInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateGroupAsync(long id, UpdateMenuGroupInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteGroupAsync(long id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<MenuGroupItemDto>> GetItemTreeAsync(long groupId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MenuGroupItemDto?> GetItemAsync(long groupId, long id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<long> CreateItemAsync(long groupId, CreateMenuGroupItemInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateItemAsync(long groupId, long id, UpdateMenuGroupItemInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteItemAsync(long groupId, long id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task BatchDeleteItemsAsync(long groupId, long[] ids, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SortItemsAsync(long groupId, List<MenuGroupItemSortInput> items, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<long>> ImportFromSystemMenuAsync(long groupId, long[] menuIds, long? parentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<NavigationMenuDto?> GetNavigationAsync(string slug, long? userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<long>> GetRoleMenuGroupIdsAsync(long roleId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetRoleMenuGroupsAsync(SetRoleMenuGroupsInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetGroupDefaultAsync(long groupId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ClientPortalDto> GetClientPortalAsync(string clientType, long? userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<GrantableMenuItemDto>> GetGrantableItemsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<long>> GetRoleMenuGroupItemIdsAsync(long roleId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetRoleMenuGroupItemsAsync(SetRoleMenuGroupItemsInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<MenuGroupItemDto>> GetItemsByModuleAsync(string module, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RemoveClientMenuItemsByModuleAsync(string moduleId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
