using System.Text.Json.Nodes;
using Ginkgo.Domain.Settings;

namespace Ginkgo.Application.Modules;

/// <summary>
/// 插件配置与 ginkgo_Sys_Settings 的桥接服务。
/// 当 module.json 声明 config.storage=database 时，配置值持久化到数据库（按 Module 隔离）。
/// </summary>
public sealed class ModuleConfigDbService
{
    private readonly ISettingsRepository _repo;

    public ModuleConfigDbService(ISettingsRepository repo)
    {
        _repo = repo;
    }

    /// <summary>构建插件配置在 Settings 表中的唯一键。</summary>
    public static string BuildKey(string moduleId, string configFile, string itemName)
        => ModuleConfigKeys.Build(moduleId, configFile, itemName);

    /// <summary>从配置样例文件将默认值写入数据库（安装时调用）。</summary>
    public async Task SeedFromSampleAsync(string moduleId, string configFile, string samplePath, CancellationToken ct = default)
    {
        if (!File.Exists(samplePath)) return;
        JsonNode? root;
        try { root = JsonNode.Parse(await File.ReadAllTextAsync(samplePath, ct)); }
        catch { return; }
        if (root is null) return;

        foreach (var item in ExtractItems(root))
        {
            var key = BuildKey(moduleId, configFile, item.Name);
            var exists = await _repo.GetAsync(key, item.Group, ct);
            if (exists != null) continue;

            var entity = Setting.Create(key, item.Value, MapItemType(item.Type), item.Title, item.Group, null);
            entity.Module = moduleId;
            await _repo.AddAsync(entity, ct);
        }
    }

    /// <summary>将数据库中的值覆盖到 groups+items 结构的 items[].value 上（管理界面读取时调用）。</summary>
    public async Task OverlayValuesFromDbAsync(JsonNode root, string moduleId, string configFile, CancellationToken ct = default)
    {
        if (root is not JsonObject obj || obj["items"] is not JsonArray items) return;
        var dbValues = await _repo.GetByModuleAsync(moduleId, ct);
        var map = dbValues
            .Where(s => s.Key.StartsWith($"{moduleId}:{configFile}:", StringComparison.Ordinal))
            .ToDictionary(
                s => s.Key[(moduleId.Length + configFile.Length + 2)..],
                s => s.Value,
                StringComparer.OrdinalIgnoreCase);

        foreach (var it in items.OfType<JsonObject>())
        {
            var name = it["name"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (map.TryGetValue(name, out var val))
                it["value"] = val is null ? null : JsonValue.Create(val);
        }
    }

    /// <summary>将管理界面提交的 groups+items 配置保存到数据库。</summary>
    public async Task SaveToDbAsync(string moduleId, string configFile, JsonNode content, long? operatorId, CancellationToken ct = default)
    {
        if (content is not JsonObject obj || obj["items"] is not JsonArray items)
            throw new InvalidOperationException("配置格式无效：缺少 items 数组");

        foreach (var it in items.OfType<JsonObject>())
        {
            var name = it["name"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(name)) continue;
            var type = it["type"]?.GetValue<string>() ?? "text";
            var title = it["title"]?.GetValue<string>();
            var group = it["group"]?.GetValue<string>();
            var value = it["value"]?.GetValue<string>();

            var key = BuildKey(moduleId, configFile, name);
            var exists = await _repo.GetAsync(key, group, ct);
            if (exists == null)
            {
                var entity = Setting.Create(key, value, MapItemType(type), title, group, operatorId);
                entity.Module = moduleId;
                await _repo.AddAsync(entity, ct);
            }
            else
            {
                exists.Module = moduleId;
                exists.SetValue(value, MapItemType(type), operatorId);
                exists.ChangeMeta(title, group, operatorId);
                await _repo.UpdateAsync(exists, ct);
            }
        }
    }

    /// <summary>从样例文件恢复数据库中的配置默认值。</summary>
    public async Task ResetFromSampleAsync(string moduleId, string configFile, string samplePath, long? operatorId, CancellationToken ct = default)
    {
        if (!File.Exists(samplePath)) return;
        JsonNode? root;
        try { root = JsonNode.Parse(await File.ReadAllTextAsync(samplePath, ct)); }
        catch { return; }
        if (root is null) return;

        foreach (var item in ExtractItems(root))
        {
            var key = BuildKey(moduleId, configFile, item.Name);
            var exists = await _repo.GetAsync(key, item.Group, ct);
            if (exists == null)
            {
                var entity = Setting.Create(key, item.Value, MapItemType(item.Type), item.Title, item.Group, operatorId);
                entity.Module = moduleId;
                await _repo.AddAsync(entity, ct);
            }
            else
            {
                exists.Module = moduleId;
                exists.SetValue(item.Value, MapItemType(item.Type), operatorId);
                exists.ChangeMeta(item.Title, item.Group, operatorId);
                await _repo.UpdateAsync(exists, ct);
            }
        }
    }

    /// <summary>对比数据库配置与样例文件初始值是否一致。</summary>
    public async Task<ModuleConfigStorageStatusResult> CompareWithSampleAsync(
        string moduleId, string configFile, string? samplePath, CancellationToken ct = default)
    {
        var result = new ModuleConfigStorageStatusResult { ConfigFile = configFile };
        var sampleItems = new Dictionary<string, ConfigItemSeed>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(samplePath) && File.Exists(samplePath))
        {
            result.SampleExists = true;
            try
            {
                var root = JsonNode.Parse(await File.ReadAllTextAsync(samplePath, ct));
                if (root != null)
                {
                    foreach (var item in ExtractItems(root))
                        sampleItems[item.Name] = item;
                }
            }
            catch { /* 样例解析失败时按无样例处理 */ }
        }

        result.SampleItemCount = sampleItems.Count;
        var prefix = $"{moduleId}:{configFile}:";
        var dbValues = await _repo.GetByModuleAsync(moduleId, ct);
        var dbMap = dbValues
            .Where(s => s.Key.StartsWith(prefix, StringComparison.Ordinal))
            .ToDictionary(
                s => s.Key[prefix.Length..],
                s => s.Value,
                StringComparer.OrdinalIgnoreCase);
        result.DbItemCount = dbMap.Count;

        foreach (var (name, seed) in sampleItems)
        {
            if (!dbMap.TryGetValue(name, out var dbVal))
                result.MissingInDb.Add(new ConfigItemDiff(name, seed.Value, null));
            else if (!ValuesEqual(seed.Value, dbVal))
                result.ValueMismatch.Add(new ConfigItemDiff(name, seed.Value, dbVal));
        }

        foreach (var (name, dbVal) in dbMap)
        {
            if (!sampleItems.ContainsKey(name))
                result.ExtraInDb.Add(new ConfigItemDiff(name, null, dbVal));
        }

        result.IsConsistent = result.MissingInDb.Count == 0
            && result.ExtraInDb.Count == 0
            && result.ValueMismatch.Count == 0;
        return result;
    }

    /// <summary>将样例文件初始配置全量同步到数据库（覆盖已有值并移除多余键）。</summary>
    public async Task<ModuleConfigSyncResult> SyncToDbFromSampleAsync(
        string moduleId, string configFile, string samplePath, long? operatorId, CancellationToken ct = default)
    {
        if (!File.Exists(samplePath))
            throw new FileNotFoundException("未找到配置样例文件", samplePath);

        await ResetFromSampleAsync(moduleId, configFile, samplePath, operatorId, ct);

        JsonNode? root;
        try { root = JsonNode.Parse(await File.ReadAllTextAsync(samplePath, ct)); }
        catch (Exception ex) { throw new InvalidOperationException($"样例文件解析失败: {ex.Message}", ex); }
        if (root is null) throw new InvalidOperationException("样例文件为空");

        var sampleNames = ExtractItems(root).Select(i => i.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var prefix = $"{moduleId}:{configFile}:";
        var dbValues = await _repo.GetByModuleAsync(moduleId, ct);
        var extraKeys = dbValues
            .Where(s => s.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Where(s => !sampleNames.Contains(s.Key[prefix.Length..]))
            .Select(s => s.Key)
            .ToList();

        var removed = 0;
        if (extraKeys.Count > 0)
            removed = await _repo.DeleteByKeysAsync(extraKeys, ct);

        var status = await CompareWithSampleAsync(moduleId, configFile, samplePath, ct);
        return new ModuleConfigSyncResult(status.SampleItemCount, removed, status.IsConsistent);
    }

    /// <summary>从数据库移除指定配置文件的全部配置项。</summary>
    public async Task<int> RemoveConfigFromDbAsync(string moduleId, string configFile, CancellationToken ct = default)
    {
        var prefix = $"{moduleId}:{configFile}:";
        return await _repo.DeleteByModuleAndKeyPrefixAsync(moduleId, prefix, ct);
    }

    private static bool ValuesEqual(string? a, string? b) =>
        string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.Ordinal);

    private static string? MapItemType(string? itemType) => itemType?.ToLowerInvariant() switch
    {
        "password" => "Password",
        "textarea" => "Text",
        "radio" or "select" or "text" or "link" or "file" or "api-selector" => "String",
        _ => "String"
    };

    private static IEnumerable<ConfigItemSeed> ExtractItems(JsonNode root)
    {
        if (root is JsonObject obj && obj["items"] is JsonArray items)
        {
            foreach (var it in items.OfType<JsonObject>())
            {
                var name = it["name"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(name)) continue;
                yield return new ConfigItemSeed(
                    name,
                    it["type"]?.GetValue<string>() ?? "text",
                    it["title"]?.GetValue<string>(),
                    it["group"]?.GetValue<string>(),
                    it["value"]?.GetValue<string>());
            }
        }
    }

    private sealed record ConfigItemSeed(string Name, string Type, string? Title, string? Group, string? Value);
}

/// <summary>插件配置与样例文件的对比结果。</summary>
public sealed class ModuleConfigStorageStatusResult
{
    public string ConfigFile { get; set; } = string.Empty;
    public bool SampleExists { get; set; }
    public int SampleItemCount { get; set; }
    public int DbItemCount { get; set; }
    public bool IsConsistent { get; set; }
    public List<ConfigItemDiff> MissingInDb { get; } = [];
    public List<ConfigItemDiff> ExtraInDb { get; } = [];
    public List<ConfigItemDiff> ValueMismatch { get; } = [];
}

/// <summary>单条配置项差异。</summary>
public sealed record ConfigItemDiff(string Name, string? SampleValue, string? DbValue);

/// <summary>同步到库操作结果。</summary>
public sealed record ModuleConfigSyncResult(int SyncedCount, int RemovedExtraCount, bool IsConsistent);
