namespace Ginkgo.Domain.Utils;

/// <summary>
/// Snowflake ID 生成器配置类。
/// 支持从配置文件和环境变量读取 MachineId。
/// </summary>
public class SnowflakeConfig
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Snowflake";

    /// <summary>
    /// 环境变量名称
    /// </summary>
    public const string MachineIdEnvVar = "SNOWFLAKE_MACHINE_ID";

    /// <summary>
    /// 机器ID（0-1023）
    /// </summary>
    public ushort MachineId { get; set; } = 0;

    /// <summary>
    /// 数据中心ID（可选，0-31）
    /// </summary>
    public byte DatacenterId { get; set; } = 0;

    /// <summary>
    /// 从环境变量获取 MachineId，如果环境变量未设置则返回 null
    /// </summary>
    public static ushort? GetMachineIdFromEnvironment()
    {
        var envValue = Environment.GetEnvironmentVariable(MachineIdEnvVar);
        if (!string.IsNullOrEmpty(envValue) && ushort.TryParse(envValue, out var machineId))
        {
            return machineId;
        }
        return null;
    }

    /// <summary>
    /// 获取有效的 MachineId（优先使用环境变量，其次使用配置值）
    /// </summary>
    public ushort GetEffectiveMachineId()
    {
        return GetMachineIdFromEnvironment() ?? MachineId;
    }

    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">当 MachineId 超出有效范围时抛出</exception>
    public void Validate()
    {
        var effectiveMachineId = GetEffectiveMachineId();
        if (effectiveMachineId > 1023)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MachineId),
                effectiveMachineId,
                "MachineId must be between 0 and 1023");
        }

        if (DatacenterId > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DatacenterId),
                DatacenterId,
                "DatacenterId must be between 0 and 31");
        }
    }
}
