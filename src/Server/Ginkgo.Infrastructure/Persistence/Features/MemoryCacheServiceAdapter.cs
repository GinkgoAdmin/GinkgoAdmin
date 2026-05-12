// 文件功能说明：
// SqlSugar 二级缓存（ICacheService）的 Memory 实现，底层基于 Microsoft.Extensions.Caching.Memory.IMemoryCache。
//
// 设计要点：
// - 仅在 Database.Features.SecondLevelCache.Enabled=true 且 Provider=Memory 时被挂载（详见 ApplySecondLevelCache）。
// - IMemoryCache 不支持枚举所有 key；本 Adapter 通过一个 ConcurrentDictionary<string, byte> 自行跟踪 key，
//   在 PostEvictionCallback 中同步清理，使 GetAllKey<V> 能返回有效 key 集合。
// - SqlSugar 的 V 范型不参与 key 隔离（key 已包含表名/参数），所以这里把所有 V 范型当作 object 处理；
//   GetAllKey<V> 只返回所有 key（用户层若要按类型过滤可自行处理）。
// - 适配 SqlSugar 5.1.4.200 的 ICacheService 接口签名；如未来 SqlSugar 升级签名有变，仅需调整本类。

using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.Features;

/// <summary>
/// 基于 <see cref="IMemoryCache"/> 的 SqlSugar 二级缓存适配器。
/// </summary>
public sealed class MemoryCacheServiceAdapter : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.Ordinal);
    private readonly int _defaultSeconds;

    /// <summary>
    /// 创建二级缓存适配器。
    /// </summary>
    /// <param name="cache">底层 IMemoryCache（由 DI 注入；框架已 <c>AddMemoryCache()</c>）。</param>
    /// <param name="defaultSeconds">默认过期秒数；SqlSugar 调用未带 cacheDurationInSeconds 的 <c>Add&lt;V&gt;</c> 时使用。</param>
    public MemoryCacheServiceAdapter(IMemoryCache cache, int defaultSeconds = 300)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _defaultSeconds = defaultSeconds > 0 ? defaultSeconds : 300;
    }

    /// <inheritdoc />
    public void Add<V>(string key, V value)
    {
        Add(key, value, _defaultSeconds);
    }

    /// <inheritdoc />
    public void Add<V>(string key, V value, int cacheDurationInSeconds)
    {
        if (string.IsNullOrEmpty(key)) return;
        var seconds = cacheDurationInSeconds > 0 ? cacheDurationInSeconds : _defaultSeconds;

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(seconds)
        };
        // 过期/驱逐时同步删除 key 跟踪表。
        options.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
        {
            if (evictedKey is string k)
            {
                _keys.TryRemove(k, out _);
            }
        });

        _cache.Set(key, value, options);
        _keys[key] = 0; // 加入 key 跟踪表（值无意义，仅占位）
    }

    /// <inheritdoc />
    public bool ContainsKey<V>(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return _cache.TryGetValue(key, out _);
    }

    /// <inheritdoc />
    public V Get<V>(string key)
    {
        if (string.IsNullOrEmpty(key)) return default!;
        if (_cache.TryGetValue(key, out var val) && val is V typed)
        {
            return typed;
        }
        return default!;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAllKey<V>()
    {
        // 直接返回快照，避免迭代时被并发修改。
        return _keys.Keys.ToArray();
    }

    /// <inheritdoc />
    public V GetOrCreate<V>(string cacheKey, Func<V> create, int cacheDurationInSeconds = int.MaxValue)
    {
        if (string.IsNullOrEmpty(cacheKey))
        {
            return create == null ? default! : create();
        }

        if (_cache.TryGetValue(cacheKey, out var val) && val is V typed)
        {
            return typed;
        }

        if (create == null) return default!;
        var fresh = create();
        // cacheDurationInSeconds 为 int.MaxValue 时按默认秒数处理，避免设置一个几十年的绝对过期时间。
        var seconds = cacheDurationInSeconds > 0 && cacheDurationInSeconds < int.MaxValue
            ? cacheDurationInSeconds
            : _defaultSeconds;
        Add(cacheKey, fresh, seconds);
        return fresh;
    }

    /// <inheritdoc />
    public void Remove<V>(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
    }
}
