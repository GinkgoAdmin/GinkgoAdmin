using Ginkgo.Application.Modules;

namespace Ginkgo.Api.Modules;

public sealed class ModuleInstallerPortAdapter : IModuleInstallerPort
{
    private readonly ModuleRepository _repo;
    private readonly ModuleInstaller _installer;

    public ModuleInstallerPortAdapter(ModuleRepository repo, ModuleInstaller installer)
    {
        _repo = repo; _installer = installer;
    }

    private ModuleRepoItem? FindById(string moduleId)
    {
        return _repo.ScanRepo().FirstOrDefault(x => string.Equals(x.Manifest.Id, moduleId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ModuleInstallResult> InstallAsync(string moduleId, CancellationToken ct = default)
    {
        var item = FindById(moduleId);
        if (item == null) return new ModuleInstallResult(false, $"模块包未找到：{moduleId}");
        var res = await _installer.InstallAsync(item, ct);
        return new ModuleInstallResult(res.Ok, res.Message);
    }

    public async Task<ModuleInstallResult> UpgradeAsync(string moduleId, CancellationToken ct = default)
    {
        var item = FindById(moduleId);
        if (item == null) return new ModuleInstallResult(false, $"模块包未找到：{moduleId}");
        var res = await _installer.UpgradeAsync(item, ct);
        return new ModuleInstallResult(res.Ok, res.Message);
    }

    public async Task<ModuleInstallResult> UninstallAsync(string moduleId, CancellationToken ct = default)
    {
        var res = await _installer.UninstallAsync(moduleId, ct);
        return new ModuleInstallResult(res.Ok, res.Message);
    }
}

