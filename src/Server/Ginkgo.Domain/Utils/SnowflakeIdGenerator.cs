using Yitter.IdGenerator;

namespace Ginkgo.Domain.Utils;

/// <summary>
/// Snowflake ID 生成器服务。
/// 封装 YitIdHelper，提供统一的 ID 生成接口。
/// </summary>
public static class SnowflakeIdGenerator
{
    private static bool _initialized = false;
    private static readonly object _lock = new();

    /// <summary>
    /// 初始化 Snowflake ID 生成器
    /// </summary>
    /// <param name="config">配置对象</param>
    /// <exception cref="InvalidOperationException">当生成器已初始化时抛出</exception>
    public static void Initialize(SnowflakeConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();

        lock (_lock)
        {
            if (_initialized)
            {
                throw new InvalidOperationException("SnowflakeIdGenerator has already been initialized");
            }

            var options = new IdGeneratorOptions(config.GetEffectiveMachineId())
            {
                // 使用默认的雪花漂移算法
                Method = 1,
                // 基础时间：2024-01-01 00:00:00 UTC
                BaseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                // 机器码位长：10位，支持最多1024个节点
                WorkerIdBitLength = 10,
                // 序列数位长：12位，每毫秒最多4096个ID
                SeqBitLength = 12
            };

            YitIdHelper.SetIdGenerator(options);
            _initialized = true;
        }
    }

    /// <summary>
    /// 初始化 Snowflake ID 生成器（使用机器ID）
    /// </summary>
    /// <param name="machineId">机器ID（0-1023）</param>
    public static void Initialize(ushort machineId = 0)
    {
        Initialize(new SnowflakeConfig { MachineId = machineId });
    }

    /// <summary>
    /// 生成新的 Snowflake ID
    /// </summary>
    /// <returns>64位整数ID</returns>
    /// <exception cref="InvalidOperationException">当生成器未初始化时抛出</exception>
    public static long NextId()
    {
        EnsureInitialized();
        return YitIdHelper.NextId();
    }

    /// <summary>
    /// 批量生成 Snowflake ID
    /// </summary>
    /// <param name="count">生成数量</param>
    /// <returns>ID 数组</returns>
    public static long[] NextIds(int count)
    {
        EnsureInitialized();
        if (count <= 0)
        {
            return Array.Empty<long>();
        }

        var ids = new long[count];
        for (int i = 0; i < count; i++)
        {
            ids[i] = YitIdHelper.NextId();
        }
        return ids;
    }

    /// <summary>
    /// 检查生成器是否已初始化
    /// </summary>
    public static bool IsInitialized => _initialized;

    /// <summary>
    /// 确保生成器已初始化
    /// </summary>
    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException(
                "SnowflakeIdGenerator has not been initialized. " +
                "Call Initialize() before generating IDs.");
        }
    }
}
