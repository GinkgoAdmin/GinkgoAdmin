using Ginkgo.Domain.Settings;

namespace Ginkgo.Application.Settings;

/// <summary>
/// 
///   (DDD)
/// </summary>
public sealed class SettingsAppService : ISettingsAppService
{
    private readonly ISettingsRepository _repo;
    public SettingsAppService(ISettingsRepository repo) { _repo = repo; }

    public async Task<List<SettingDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _repo.GetAllAsync(ct);
        return list.Select(x => new SettingDto
        {
            Key = x.Key,
            Value = x.Value,
            Type = x.Type,
            Description = x.Description,
            Class = x.Class,
            Version = x.Version
        }).ToList();
    }

    public async Task UpsertAsync(SettingDto input, long? operatorId, CancellationToken ct = default)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (string.IsNullOrWhiteSpace(input.Key)) throw new ArgumentException("Key 不能为空", nameof(input.Key));

        var key = input.Key.Trim();
        var @class = string.IsNullOrWhiteSpace(input.Class) ? null : input.Class!.Trim();

        var exists = await _repo.GetAsync(key, @class, ct);
        if (exists == null)
        {
            var entity = Setting.Create(key, input.Value, input.Type, input.Description, @class, operatorId);
            await _repo.AddAsync(entity, ct);
            return;
        }

        exists.SetValue(input.Value, input.Type, operatorId);
        exists.ChangeMeta(input.Description, @class, operatorId);
        await _repo.UpdateAsync(exists, ct);
    }
}

