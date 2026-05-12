using Ginkgo.Domain;
using Ginkgo.Plugin.Abstractions.Extensions;
using SqlSugar;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ginkgo.ServerToolkit; // ICurrentUser

namespace Ginkgo.Infrastructure.Persistence;

public class SqlSugarRepository<T> : IRepository<T> where T : Entity, new()
{
    private readonly ISqlSugarClient _db;
    private readonly ICurrentUser? _currentUser; // 可空：保持构造兼容
    private readonly IOptionsMonitor<DataScopeOptions>? _scopeOptions;
    private readonly IServiceProvider? _serviceProvider;
    // 数据范围解析器：优先从 DI 解析（可享受 DB Settings + DB Roles 桥接能力）；DI 不可用时回退到内置 DataScopeProvider
    private IDataScopeResolver? _resolver;
    private bool _resolverInitialized;

    public SqlSugarRepository(ISqlSugarClient db, ICurrentUser? currentUser = null, IOptionsMonitor<DataScopeOptions>? scopeOptions = null, IServiceProvider? serviceProvider = null)
    {
        _db = db;
        _currentUser = currentUser;
        _scopeOptions = scopeOptions;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 懒加载数据范围解析器：
    ///   1) 优先 _serviceProvider.GetService&lt;IDataScopeResolver&gt;()——含 ISettingsRepository 依赖、能读 DB Settings 与 DB Roles 桥接
    ///   2) DI 中没有时回退用 IOptionsMonitor + ICurrentUser 直接 new DataScopeProvider（不读 DB，仅 appsettings）
    /// 该方法保证 DataScopeProvider/IDataScopeResolver 在每个 SqlSugarRepository&lt;T&gt; 实例内只解析一次。
    /// </summary>
    private IDataScopeResolver? GetResolver()
    {
        if (_resolverInitialized) return _resolver;
        _resolverInitialized = true;
        if (_currentUser == null) return _resolver;
        try
        {
            if (_serviceProvider != null)
            {
                _resolver = _serviceProvider.GetService<IDataScopeResolver>();
            }
        }
        catch { /* 忽略 DI 解析异常，落到回退路径 */ }
        if (_resolver == null && _scopeOptions != null)
        {
            _resolver = new DataScopeProvider(_scopeOptions, _db, _currentUser);
        }
        return _resolver;
    }

    /// <summary>
    /// 调用所有已注册的实体变更拦截器。
    /// </summary>
    private void InvokeInterceptors(object entity, bool isInsert)
    {
        if (_serviceProvider == null) return;
        var interceptors = _serviceProvider.GetServices<IEntityChangeInterceptor>()
            ?.OrderBy(i => i.Order)
            .ToList();
        if (interceptors == null) return;
        foreach (var interceptor in interceptors)
        {
            try
            {
                if (isInsert)
                    interceptor.OnInserting(entity, _serviceProvider);
                else
                    interceptor.OnUpdating(entity, _serviceProvider);
            }
            catch { /* 拦截器异常不影响主流程 */ }
        }
    }

    public ISugarQueryable<T> Query()
    {
        // 统一过滤软删除（若为审计实体）
        var q = _db.Queryable<T>();
        if (typeof(AuditableEntity).IsAssignableFrom(typeof(T)))
        {
            // 使用动态条件避免类型转换问题
            q = q.Where("IsDeleted = @IsDeleted", new { IsDeleted = false });
        }

        // 数据范围自动过滤（启用开关由 IDataScopeResolver.IsEnabled() 决定：
        //   优先 DB Settings.DataPermission.Enabled；未配置时回退 appsettings DataScope:Enabled，默认关闭确保兼容）
        var resolver = GetResolver();
        if (resolver != null && _currentUser?.IsAuthenticated == true && resolver.IsEnabled())
        {
            var eff = resolver.Resolve();
            if (!eff.IsAll)
            {
                var hasDeptProp = typeof(T).GetProperty("DepartmentId") != null;
                var condParts = new List<string>();
                var param = new List<SugarParameter>();

                if (hasDeptProp && eff.DepartmentIds.Count > 0)
                {
                    condParts.Add("DepartmentId IN (@deptIds)");
                    param.Add(new SugarParameter("@deptIds", eff.DepartmentIds.ToArray()));
                }
                if (eff.IncludeOwn && typeof(AuditableEntity).IsAssignableFrom(typeof(T)) && _currentUser.Id != null)
                {
                    condParts.Add("CreatedBy = @uid");
                    param.Add(new SugarParameter("@uid", _currentUser.Id));
                }
                if (condParts.Count > 0)
                {
                    var cond = string.Join(" OR ", condParts);
                    q = q.Where(cond, param.ToArray());
                }
            }
        }
        return q;
    }

    public async Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<T>().Where(x => x.Id == id);

        // 应用软删除过滤
        if (typeof(AuditableEntity).IsAssignableFrom(typeof(T)))
        {
            // 使用动态条件避免类型转换问题
            query = query.Where("IsDeleted = @IsDeleted", new { IsDeleted = false });
        }

        return await query.FirstAsync();
    }

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        // 自动填充审计字段
        if (entity is AuditableEntity aud)
        {
            if (aud.CreatedAt == default) aud.CreatedAt = DateTime.Now;
            aud.IsDeleted = false; // 确保新实体不是删除状态
        }

        // 模块扩展点：实体插入前拦截
        InvokeInterceptors(entity, isInsert: true);

        await _db.Insertable(entity).IgnoreColumns("RowVersion").ExecuteCommandAsync();
    }

    public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity is AuditableEntity aud)
        {
            aud.UpdatedAt = DateTime.Now;
        }

        // 模块扩展点：实体更新前拦截
        InvokeInterceptors(entity, isInsert: false);

        var affected = await _db.Updateable(entity)
            .IgnoreColumns("RowVersion")
            .Where(x => x.Id == entity.Id)
            .ExecuteCommandAsync();
        if (affected == 0)
        {
            throw new InvalidOperationException($"更新失败：未找到要更新的记录（{typeof(T).Name}:{entity.Id}）");
        }
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (typeof(AuditableEntity).IsAssignableFrom(typeof(T)))
        {
            // 软删除
            var entity = await GetByIdAsync(id, cancellationToken);
            if (entity == null) return;

            // 自动处理唯一索引列：追加 _deleted_{id} 后缀，避免软删除后新建同值记录时唯一约束冲突
            await ClearUniqueColumnsBeforeSoftDelete(entity);

            var aud = (AuditableEntity)(object)entity;
            aud.IsDeleted = true;
            aud.UpdatedAt = DateTime.Now;
            aud.DeletedAt = DateTime.Now;
            if (_currentUser?.Id != null)
            {
                aud.DeletedBy = _currentUser.Id;
            }
            await _db.Updateable((T)(object)aud).IgnoreColumns("RowVersion").ExecuteCommandAsync();
        }
        else
        {
            // 物理删除
            await _db.Deleteable<T>().In(id).ExecuteCommandAsync();
        }
    }

    /// <summary>
    /// 软删除前，自动为有唯一索引的 string 列追加 _deleted_{id}，避免唯一约束冲突。
    /// </summary>
    private async Task ClearUniqueColumnsBeforeSoftDelete(T entity)
    {
        try
        {
            // 获取实体对应的表名
            var entityInfo = _db.EntityMaintenance.GetEntityInfo<T>();
            var tableName = entityInfo.DbTableName;

            // 查询该表所有唯一索引的列（排除主键）
            var sql = @"SELECT COLUMN_NAME FROM information_schema.STATISTICS 
                        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName 
                        AND NON_UNIQUE = 0 AND INDEX_NAME != 'PRIMARY'";
            var dt = await _db.Ado.GetDataTableAsync(sql, new { tableName });

            if (dt.Rows.Count == 0) return;

            var suffix = $"_deleted_{entity.Id}";
            var modified = false;

            foreach (System.Data.DataRow row in dt.Rows)
            {
                var colName = row["COLUMN_NAME"]?.ToString();
                if (string.IsNullOrEmpty(colName)) continue;

                // 通过列名找对应的实体属性（不区分大小写）
                var prop = typeof(T).GetProperty(colName, 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (prop == null || prop.PropertyType != typeof(string) || !prop.CanWrite) continue;

                var currentVal = prop.GetValue(entity) as string;
                if (string.IsNullOrEmpty(currentVal) || currentVal.Contains("_deleted_")) continue;

                prop.SetValue(entity, currentVal + suffix);
                modified = true;
            }

            if (modified)
            {
                await _db.Updateable(entity).IgnoreColumns("RowVersion").ExecuteCommandAsync();
            }
        }
        catch
        {
            // 如果自动清理失败，不影响软删除主流程
        }
    }

    public async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var entityList = entities.ToList();
        if (!entityList.Any()) return;

        // 批量填充审计字段
        foreach (var entity in entityList)
        {
            if (entity is AuditableEntity aud)
            {
                if (aud.CreatedAt == default) aud.CreatedAt = DateTime.Now;
                aud.IsDeleted = false;
            }
        }

        await _db.Insertable(entityList).IgnoreColumns("RowVersion").ExecuteCommandAsync();
    }

    public async Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var entityList = entities.ToList();
        if (!entityList.Any()) return;

        // 批量填充审计字段
        foreach (var entity in entityList)
        {
            if (entity is AuditableEntity aud)
            {
                aud.UpdatedAt = DateTime.Now;
            }
        }

        await _db.Updateable(entityList).IgnoreColumns("RowVersion").ExecuteCommandAsync();
    }

    public async Task DeleteRangeAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        if (!idList.Any()) return;

        if (typeof(AuditableEntity).IsAssignableFrom(typeof(T)))
        {
            // 批量软删除
            var entities = await _db.Queryable<T>()
                .Where(x => idList.Contains(x.Id))
                .ToListAsync();

            foreach (var entity in entities)
            {
                // 自动处理唯一索引列
                await ClearUniqueColumnsBeforeSoftDelete(entity);

                if (entity is AuditableEntity aud)
                {
                    aud.IsDeleted = true;
                    aud.UpdatedAt = DateTime.Now;
                    aud.DeletedAt = DateTime.Now;
                    if (_currentUser?.Id != null)
                    {
                        aud.DeletedBy = _currentUser.Id;
                    }
                }
            }

            await _db.Updateable(entities).IgnoreColumns("RowVersion").ExecuteCommandAsync();
        }
        else
        {
            // 批量物理删除
            await _db.Deleteable<T>().In(idList).ExecuteCommandAsync();
        }
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<T>();

        // 应用软删除过滤
        if (typeof(AuditableEntity).IsAssignableFrom(typeof(T)))
        {
            // 使用动态条件避免类型转换问题
            query = query.Where("IsDeleted = @IsDeleted", new { IsDeleted = false });
        }

        return await query.CountAsync();
    }

    public async Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<T>().Where(x => x.Id == id);

        // 应用软删除过滤
        if (typeof(AuditableEntity).IsAssignableFrom(typeof(T)))
        {
            // 使用动态条件避免类型转换问题
            query = query.Where("IsDeleted = @IsDeleted", new { IsDeleted = false });
        }

        return await query.AnyAsync();
    }

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<T>();

        // 应用软删除过滤
        if (typeof(AuditableEntity).IsAssignableFrom(typeof(T)))
        {
            // 使用动态条件避免类型转换问题
            query = query.Where("IsDeleted = @IsDeleted", new { IsDeleted = false });
        }

        return await query.ToListAsync();
    }
}

