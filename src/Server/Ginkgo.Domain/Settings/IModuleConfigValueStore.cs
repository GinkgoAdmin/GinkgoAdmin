namespace Ginkgo.Domain.Settings;

/// <summary>
/// 插件模块配置值读取（数据库存储模式）。
/// 插件在 module.json 声明 config.storage=database 时，运行期通过此接口读取配置值。
/// </summary>
public interface IModuleConfigValueStore
{
    /// <summary>读取单个配置项值。</summary>
    Task<string?> GetValueAsync(string moduleId, string configFile, string itemName, CancellationToken ct = default);

    /// <summary>读取模块某配置文件下的全部配置项（itemName → value）。</summary>
    Task<IReadOnlyDictionary<string, string?>> GetAllValuesAsync(string moduleId, string configFile, CancellationToken ct = default);
}
