// 文件功能说明：
// 提供依赖注入扩展，注册 SqlSugarClient 与通用仓储实现。
// 同时承载 SqlSugar 高级能力的开关化挂载（详见 document/SqlSugar 性能优化建议.md §〇）。

using Ginkgo.Domain;
using Ginkgo.Infrastructure.Abstractions;
using Ginkgo.Infrastructure.Dialects;
using Ginkgo.Infrastructure.Persistence.Features;
using Ginkgo.Plugin.Abstractions.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.Extensions;

/// <summary>
/// 依赖注入扩展方法。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册通用仓储实现（IRepository&lt;T&gt; → SqlSugarRepository&lt;T&gt;）。
    /// </summary>
    public static IServiceCollection AddGinkgoPersistence(this IServiceCollection services)
    {
        services.AddScoped(typeof(IRepository<>), typeof(SqlSugarRepository<>));
        return services;
    }

    /// <summary>
    /// 注册主框架内置的数据库方言（MySql / SqlServer / PostgreSql）以及 <see cref="IDialectRegistry"/> 默认实现。
    /// 后续新增数据库（Oracle / 达梦 等）只需在此处追加一行 AddSingleton。
    /// </summary>
    public static IServiceCollection AddGinkgoDatabaseDialects(this IServiceCollection services)
    {
        // 内置方言（Singleton：无状态、线程安全）
        services.AddSingleton<IDatabaseDialect, MySqlDialect>();

        // 方言注册中心
        services.AddSingleton<IDialectRegistry, DialectRegistry>();
        return services;
    }

    /// <summary>
    /// 注册并配置 SqlSugarClient（根据配置自动选择数据库类型，并按 <c>Database.Features</c> 开关挂载高级能力）。
    /// </summary>
    public static IServiceCollection AddGinkgoSqlSugarByConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // 先确保方言注册中心已就位（多次调用幂等：AddSingleton 不会重复注册同一实现类型实例）
        services.AddGinkgoDatabaseDialects();

        // 强类型绑定 Database:Features 开关；未配置或缺省时所有 Enabled 子项保持默认（多数为 false，BulkOps/SlowQuery 默认 true 兼容现状）。
        services.Configure<DatabaseFeaturesOptions>(configuration.GetSection(DatabaseFeaturesOptions.SectionName));

        // SlowQuery 异步落 OpLog 后台基础设施：
        // - SlowQueryReporter 单例承载内部 Channel；运行时 SlowQuery 启用 + WriteToOpLog=true 时由 ApplySlowQuery 投递事件。
        // - SlowQueryHostedService 单实例后台消费 Channel 写 OpLog；总是注册（生命周期由 IHost 管理），
        //   未启用 SlowQuery 或 WriteToOpLog=false 时通道不会有事件，HostedService 阻塞在 ReadAllAsync 上零开销。
        services.AddSingleton<SlowQueryReporter>();
        services.AddHostedService<SlowQueryHostedService>();

        // ISqlSugarClient 改为 Singleton：SqlSugar 内部线程安全（IsAutoCloseConnection=true 保证连接自动释放）。
        // 原 Scoped 注册会导致 TenantDbRouter（Singleton）无法在构造函数注入，触发 DI scope validation 错误。
        services.AddSingleton<ISqlSugarClient>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetService<ILogger<SqlSugarClient>>();
            var registry = sp.GetRequiredService<IDialectRegistry>();
            var features = sp.GetRequiredService<IOptions<DatabaseFeaturesOptions>>().Value;
            var provider = cfg["Database:Provider"];
            var cs = cfg.GetConnectionString("Default") ?? cfg["Database:ConnectionString"] ?? string.Empty;

            if (string.IsNullOrWhiteSpace(provider))
            {
                throw new InvalidOperationException("未找到数据库提供者配置，请在配置中设置 Database:Provider（例如 MySql 或 SqlServer）以及相应的连接字符串！");
            }

            // 通过方言注册中心解析。未注册的 provider 会抛出包含已注册清单的明确异常。
            var dialect = registry.Get(provider);
            var dbType = dialect.SqlSugarDbType;

            var config = new ConnectionConfig
            {
                ConnectionString = cs,
                DbType = dbType,
                IsAutoCloseConnection = true, // 开启自动开关连接，避免并发下共享连接导致 DataReader 冲突
                InitKeyType = InitKeyType.Attribute,
                ConfigureExternalServices = new ConfigureExternalServices
                {
                    // 实体配置
                    EntityService = (c, p) =>
                    {
                        // 自动转换下划线命名
                        if (!string.IsNullOrEmpty(p.DbColumnName) && p.DbColumnName.ToLower().Contains("_"))
                        {
                            p.DbColumnName = p.DbColumnName.ToLower();
                        }
                    },
                    // 实体命名服务
                    EntityNameService = (type, entity) =>
                    {
                        // 可以在这里自定义表名映射规则
                    }
                }
            };

            // ===== 高级能力：构造期挂载（与 ConnectionConfig 相关、必须在 SqlSugarClient 实例化前完成） =====
            ApplyReadWriteSplit(config, features.ReadWriteSplit, logger);          // P1.1 ✅ Stage B
            ApplySecondLevelCache(config, features.SecondLevelCache, logger, sp);  // P1.2 ✅ Stage B
            // 注：SaasMultiDb 必须在 SqlSugarClient 实例化之后调用（需要 AsTenant()），已移至下方 client 初始化后执行。

            var client = new SqlSugarClient(config);

            // 设置命令超时时间
            client.Ado.CommandTimeOut = 30;

            // 配置日志
            if (logger != null)
            {
                client.Aop.OnLogExecuting = (sql, pars) =>
                {
                    // 记录SQL执行日志
                    var paramStr = string.Join(", ", pars?.Select(p => $"{p.ParameterName}={p.Value}") ?? Array.Empty<string>());
                    logger.LogDebug("SqlSugar执行SQL: {Sql} | 参数: {Parameters}", sql, paramStr);
                };

                client.Aop.OnError = (exp) =>
                {
                    // 记录SQL错误日志
                    logger.LogError(exp, "SqlSugar执行出错: {Message}", exp.Message);
                };
            }

            // 配置数据读取事件，用于调试绑定问题
            client.Aop.DataExecuting = (oldValue, entityInfo) =>
            {
                // 可以在这里查看数据绑定过程
            };

            // ===== 高级能力：客户端期挂载（基于 client.Aop） =====
            ApplySlowQuery(client, features.SlowQuery, logger, sp);                // P0.2 ✅ Stage A
            ApplySaasMultiDb(client, features.SaasMultiDb, dbType, logger);          // P2.2 ✅ Stage C
            ApplySplitTable(client, features.SplitTable, logger);                  // P2.1 ✅ Stage C
            ApplyReportable(features.Reportable, logger);                          // P2.3（C 阶段填充：纯文档语义）

            // ===== 模块扩展点：ISqlSugarConfigurator =====
            // 收集所有模块注册的 SqlSugar 配置器，按 Order 排序后依次调用。
            // 模块可在此扩展点添加 QueryFilter（如租户隔离）、AOP 等，无需修改此文件。
            var configurators = sp.GetServices<ISqlSugarConfigurator>()
                ?.OrderBy(c => c.Order)
                .ToList();
            if (configurators != null)
            {
                foreach (var configurator in configurators)
                {
                    try
                    {
                        configurator.Configure(client, sp);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "ISqlSugarConfigurator '{Type}' 执行失败", configurator.GetType().Name);
                    }
                }
            }

            return client;
        });
        return services;
    }

    // ============================================================================
    // SqlSugar 高级能力开关挂载（按 Database.Features.<Feature>.Enabled 条件化）
    // 设计原则（详见 document/SqlSugar 性能优化建议.md §〇）：
    //   - Enabled=false 或子配置为空时**完全不挂载**，行为与未引入前一致、零开销。
    //   - 任何能力都不能产生上线后无法关闭的强制副作用。
    //   - 阶段性填充：A 阶段已落地 SlowQuery；B 阶段填充 ReadWriteSplit / SecondLevelCache；C 阶段填充其余。
    // ============================================================================

    /// <summary>
    /// P0.2 慢查询：按 <c>SlowQueryOptions.Enabled</c> + <c>ThresholdMs</c> 挂载 OnLogExecuted 回调；
    /// <c>WriteToOpLog=true</c> 时通过 SlowQueryReporter 异步落 OpLog（避免阻塞 SQL 链路）。
    /// </summary>
    private static void ApplySlowQuery(
        ISqlSugarClient client,
        SlowQueryOptions options,
        ILogger? logger,
        IServiceProvider sp)
    {
        if (options == null || !options.Enabled)
        {
            return; // 完全不挂回调；零开销
        }

        var threshold = options.ThresholdMs > 0 ? options.ThresholdMs : 1000;
        var writeToOpLog = options.WriteToOpLog;
        var reporter = writeToOpLog ? sp.GetService<SlowQueryReporter>() : null;

        client.Aop.OnLogExecuted = (sql, pars) =>
        {
            try
            {
                var elapsedMs = client.Ado.SqlExecutionTime.TotalMilliseconds;
                if (elapsedMs <= threshold)
                {
                    return;
                }

                logger?.LogWarning(
                    "SqlSugar 慢查询：执行时间 {ElapsedMs}ms（阈值 {ThresholdMs}ms） | SQL: {Sql}",
                    elapsedMs, threshold, sql);

                if (reporter != null)
                {
                    reporter.Enqueue(new SlowQueryEvent(
                        At: DateTime.Now,
                        Sql: sql ?? string.Empty,
                        ElapsedMs: (long)elapsedMs,
                        ThresholdMs: threshold,
                        UserId: null));
                }
            }
            catch
            {
                // 慢查询监控本身的异常不允许影响 SQL 主流程。
            }
        };
    }

    /// <summary>
    /// P1.1 读写分离：按 <c>ReadWriteSplitOptions.Enabled</c> + <c>Slaves</c> 把 <c>SlaveConnectionConfigs</c> 填入 <see cref="ConnectionConfig"/>。
    /// SqlSugar 内部会按 <c>HitRate</c> 随机分发查询到从库，写自动走主库；事务（<c>UseTranAsync</c>）内部全部走主。
    /// 仅在 <c>Enabled=true</c> 且 <c>Slaves</c> 非空时挂载，否则保持单主库默认行为。
    /// </summary>
    internal static void ApplyReadWriteSplit(
        ConnectionConfig config,
        ReadWriteSplitOptions options,
        ILogger? logger)
    {
        if (options == null || !options.Enabled || options.Slaves == null || options.Slaves.Count == 0)
        {
            return;
        }

        var slaves = new List<SlaveConnectionConfig>(options.Slaves.Count);
        var invalidIndex = 0;
        foreach (var s in options.Slaves)
        {
            invalidIndex++;
            if (s == null || string.IsNullOrWhiteSpace(s.ConnectionString))
            {
                // 跳过空配置，但提示运维。空配置不会阻塞主库正常运行。
                logger?.LogWarning(
                    "[Features.ReadWriteSplit] 第 {Index} 个 Slave 缺少 ConnectionString，已跳过。",
                    invalidIndex);
                continue;
            }

            // HitRate 必须 > 0（SqlSugar 内部用其作权重和；0 或负值会让该从库永不被命中）。
            var hitRate = s.HitRate > 0 ? s.HitRate : 10;
            slaves.Add(new SlaveConnectionConfig
            {
                ConnectionString = s.ConnectionString,
                HitRate = hitRate,
            });
        }

        if (slaves.Count == 0)
        {
            // 全部从库配置都无效；保持主库单库行为，记录 Warning。
            logger?.LogWarning("[Features.ReadWriteSplit] 已声明启用但没有任何有效的从库配置；按单主库模式继续运行。");
            return;
        }

        config.SlaveConnectionConfigs = slaves;
        logger?.LogInformation(
            "[Features.ReadWriteSplit] 已挂载 {Count} 个从库；SqlSugar 将按 HitRate 随机分发查询，写自动走主库。",
            slaves.Count);
    }

    /// <summary>
    /// P1.2 二级缓存：按 <c>SecondLevelCacheOptions.Enabled</c> + <c>Provider</c> 把 SqlSugar 的 <c>ICacheService</c>
    /// 注入到 <c>ConnectionConfig.ConfigureExternalServices.DataInfoCacheService</c>（构造期挂载），
    /// 之后业务代码 <c>.WithCache(seconds)</c> 才会真实生效；写入后需调用 <c>.RemoveDataCache()</c> 避免脏读。
    /// Provider=Memory 时使用 <see cref="MemoryCacheServiceAdapter"/>（底层 <c>IMemoryCache</c>）；
    /// 其他 Provider（如 Redis）留作后续版本，启用后会记录 Warning 并不挂载。
    /// </summary>
    internal static void ApplySecondLevelCache(
        ConnectionConfig config,
        SecondLevelCacheOptions options,
        ILogger? logger,
        IServiceProvider sp)
    {
        if (options == null || !options.Enabled)
        {
            return;
        }

        var provider = (options.Provider ?? string.Empty).Trim();
        if (!string.Equals(provider, "Memory", StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogWarning(
                "[Features.SecondLevelCache] Provider='{Provider}' 暂不支持，仅 Memory 已落地；本次未挂载。",
                provider);
            return;
        }

        var memoryCache = sp.GetService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
        if (memoryCache == null)
        {
            logger?.LogWarning("[Features.SecondLevelCache] 未在 DI 容器中解析到 IMemoryCache，二级缓存未挂载；请确保 services.AddMemoryCache() 已调用。");
            return;
        }

        var defaultSeconds = options.DefaultSeconds > 0 ? options.DefaultSeconds : 300;
        var adapter = new MemoryCacheServiceAdapter(memoryCache, defaultSeconds);

        // SqlSugar 5.x：ConnectionConfig.ConfigureExternalServices.DataInfoCacheService 是设置二级缓存提供者的正式入口。
        config.ConfigureExternalServices ??= new ConfigureExternalServices();
        config.ConfigureExternalServices.DataInfoCacheService = adapter;

        logger?.LogInformation(
            "[Features.SecondLevelCache] 已挂载 Memory 二级缓存（DefaultSeconds={DefaultSeconds}）；调用 .WithCache() 后真实生效。",
            defaultSeconds);
    }

    /// <summary>
    /// P2.1 自动分表（Stage C 已落地）：按 <c>SplitTableOptions.Enabled</c> 提供 <see cref="Ginkgo.Infrastructure.Abstractions.ISplitTableContext"/> 封装。
    /// SqlSugar 分表由实体 <c>[SplitTable]</c> + <c>[SplitField]</c> 特性驱动，本方法仅输出日志确认。
    /// </summary>
    private static void ApplySplitTable(
        ISqlSugarClient client,
        SplitTableOptions options,
        ILogger? logger)
    {
        if (options == null || !options.Enabled)
        {
            return;
        }

        logger?.LogInformation(
            "[Features.SplitTable] 已启用（Strategy={Strategy}）。实体需配合 [SplitTable] + [SplitField] 特性，业务侧通过 ISplitTableContext 统一 CRUD。",
            options.Strategy);
    }

    /// <summary>
    /// P2.2 SaaS 多库（Stage C 已落地）：启用后通过 <c>ISqlSugarClient.AsTenant().AddConnection()</c>
    /// 注册 db.json 中声明的租户库连接，业务侧通过 <see cref="Ginkgo.Infrastructure.Abstractions.ITenantDbRouter"/> 切库。
    /// </summary>
    private static void ApplySaasMultiDb(
        ISqlSugarClient client,
        SaasMultiDbOptions options,
        DbType dbType,
        ILogger? logger)
    {
        if (options == null || !options.Enabled)
        {
            return;
        }

        var connections = options.Connections;
        if (connections == null || connections.Count == 0)
        {
            logger?.LogWarning(
                "[Features.SaasMultiDb] 已启用但未声明任何 Connections。请在 db.json SaasMultiDb.Connections 中添加租户库配置。");
            return;
        }

        var tenant = client.AsTenant();
        var registered = 0;

        foreach (var conn in connections)
        {
            if (string.IsNullOrWhiteSpace(conn.ConfigId) || string.IsNullOrWhiteSpace(conn.ConnectionString))
            {
                logger?.LogWarning(
                    "[Features.SaasMultiDb] 跳过无效连接声明（ConfigId 或 ConnectionString 为空）。");
                continue;
            }

            tenant.AddConnection(new ConnectionConfig
            {
                ConfigId = conn.ConfigId,
                ConnectionString = conn.ConnectionString,
                DbType = dbType,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
            });
            registered++;

            logger?.LogDebug(
                "[Features.SaasMultiDb] 已注册租户库 ConfigId={ConfigId}（{Description}）",
                conn.ConfigId, conn.Description);
        }

        logger?.LogInformation(
            "[Features.SaasMultiDb] 已启用，共注册 {Count} 个租户库连接。业务侧通过 ITenantDbRouter.ChangeDatabase(configId) 切库。",
            registered);
    }

    /// <summary>
    /// P2.3 Reportable：报表查询入口（Stage C 已落地）。本身是 SqlSugar 查询时方法链 <c>db.Reportable&lt;T&gt;()</c>，
    /// 此处不需要在 client 上挂载；运行时由 <see cref="Ginkgo.Infrastructure.Abstractions.IReportableQueryFactory"/> 接管，
    /// Disabled 时其 Create 方法抛 <see cref="NotSupportedException"/>。
    /// </summary>
    private static void ApplyReportable(
        ReportableOptions options,
        ILogger? logger)
    {
        if (options == null || !options.Enabled)
        {
            return;
        }

        logger?.LogDebug(
            "[Features.Reportable] 已声明启用，运行时由 IReportableQueryFactory 接管 db.Reportable<T>(list)。");
    }

    /// <summary>
    /// 从配置选择数据库提供程序，并完成 SqlSugar 与仓储的注册。
    /// </summary>
    public static IServiceCollection AddGinkgoDbByConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // 注册 SqlSugar 客户端（含 Database.Features 开关绑定与高级能力挂载）
        services.AddGinkgoSqlSugarByConfiguration(configuration);

        // 注册通用仓储
        services.AddGinkgoPersistence();

        // 大数据写入服务（由 Database.Features.BulkOps 控制；默认启用以兼容现状 ImportController）
        services.AddScoped<Ginkgo.Infrastructure.Abstractions.IBulkInsertService,
            Ginkgo.Infrastructure.Persistence.Features.BulkInsertService>();

        // 通用分批迭代查询服务（无开关；P1.3 Stage B）
        services.AddScoped<Ginkgo.Infrastructure.Persistence.Features.IIterativeQueryService,
            Ginkgo.Infrastructure.Persistence.Features.IterativeQueryService>();

        // 并发数据库操作执行器（由 Database.Features.Concurrency 控制；P2.4 Stage C）
        services.AddScoped<Ginkgo.Infrastructure.Abstractions.IConcurrentDbExecutor,
            Ginkgo.Infrastructure.Persistence.Features.ConcurrentDbExecutor>();

        // 分表操作上下文（由 Database.Features.SplitTable 控制；P2.1 Stage C）
        services.AddScoped<Ginkgo.Infrastructure.Abstractions.ISplitTableContext,
            Ginkgo.Infrastructure.Persistence.Features.SplitTableContext>();

        // SaaS 多库路由器（由 Database.Features.SaasMultiDb 控制；P2.2 Stage C）
        // 注意：TenantDbRouter 需要在运行时动态注入租户连接，必须按 Singleton 注册，
        // 否则每个 Scope 都会重建并丢失已经注册过的连接列表。
        services.AddSingleton<Ginkgo.Infrastructure.Abstractions.ITenantDbRouter,
            Ginkgo.Infrastructure.Persistence.Features.TenantDbRouter>();

        // 敏感字段保护器（AES-256-GCM）。租户连接串、第三方密钥等需要密文落库的场景统一使用。
        services.AddSingleton<Ginkgo.Infrastructure.Abstractions.IConnectionSecretProtector,
            Ginkgo.Infrastructure.Security.AesGcmSecretProtector>();

        // Reportable 报表查询入口（由 Database.Features.Reportable 控制；P2.3 Stage C）
        services.AddScoped<Ginkgo.Infrastructure.Abstractions.IReportableQueryFactory,
            Ginkgo.Infrastructure.Persistence.Features.ReportableQueryFactory>();

        // 索引 DDL 生成器（无开关；P2.5 Stage C）
        services.AddScoped<Ginkgo.Infrastructure.Abstractions.IIndexDdlGenerator>(sp =>
        {
            var dialectRegistry = sp.GetRequiredService<Ginkgo.Infrastructure.Abstractions.IDialectRegistry>();
            var providerName = sp.GetRequiredService<IConfiguration>()["Database:Provider"] ?? "MySql";
            var dialect = dialectRegistry.Get(providerName);
            return new Ginkgo.Infrastructure.Persistence.Features.IndexDdlGenerator(dialect);
        });

        // 领域仓储注册（示例：操作日志）
        services.AddScoped<Ginkgo.Domain.Repositories.IOpLogRepository, Ginkgo.Infrastructure.Persistence.SqlSugar.OpLogRepository>();
        // 领域服务注册：角色数据范围
        // 领域仓储注册：角色/权限/菜单
        services.AddScoped<Ginkgo.Domain.Roles.IRoleRepository, Ginkgo.Infrastructure.Persistence.SqlSugar.RoleRepository>();
        services.AddScoped<Ginkgo.Domain.Roles.IRolePermissionRepository, Ginkgo.Infrastructure.Persistence.SqlSugar.RolePermissionRepository>();
        services.AddScoped<Ginkgo.Domain.Menus.IMenuRepository, Ginkgo.Infrastructure.Persistence.SqlSugar.MenuRepository>();
        services.AddScoped<Ginkgo.Domain.Permissions.IPermissionRepository, Ginkgo.Infrastructure.Persistence.SqlSugar.PermissionRepository>();
        // System Settings 仓储
        services.AddScoped<Ginkgo.Domain.Settings.ISettingsRepository, Ginkgo.Infrastructure.Persistence.SqlSugar.SettingsRepository>();


        // Department/User 仓储与服务
        services.AddScoped<Ginkgo.Domain.Departments.IDepartmentRepository, Ginkgo.Infrastructure.Persistence.SqlSugar.DepartmentRepository>();
        services.AddScoped<Ginkgo.Domain.Users.IUserRepository, Ginkgo.Infrastructure.Persistence.SqlSugar.UserRepository>();
        services.AddScoped<Ginkgo.Domain.Users.IUserRoleRepository, Ginkgo.Infrastructure.Persistence.SqlSugar.UserRoleRepository>();
        services.AddScoped<Ginkgo.Domain.Users.IUserDepartmentRepository, Ginkgo.Infrastructure.Persistence.SqlSugar.UserDepartmentRepository>();

        services.AddScoped<Ginkgo.Domain.Users.IPasswordHasher, Ginkgo.Infrastructure.Security.PasswordHasher>();

        services.AddScoped<Ginkgo.Domain.Roles.IRoleDataScopeService, Ginkgo.Infrastructure.Persistence.Services.RoleDataScopeService>();

        return services;
    }
}

