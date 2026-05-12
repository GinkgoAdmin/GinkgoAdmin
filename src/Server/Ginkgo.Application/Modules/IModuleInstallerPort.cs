namespace Ginkgo.Application.Modules;

public sealed record ModuleInstallResult(bool Ok, string Message);

public interface IModuleInstallerPort
{
    Task<ModuleInstallResult> InstallAsync(string moduleId, CancellationToken ct = default);
    Task<ModuleInstallResult> UpgradeAsync(string moduleId, CancellationToken ct = default);
    Task<ModuleInstallResult> UninstallAsync(string moduleId, CancellationToken ct = default);
}

