using System.Threading;

namespace Ginkgo.Application.Settings;

public interface ISettingsAppService
{
    Task<List<SettingDto>> GetAllAsync(CancellationToken ct = default);
    Task UpsertAsync(SettingDto input, long? operatorId, CancellationToken ct = default);
}

