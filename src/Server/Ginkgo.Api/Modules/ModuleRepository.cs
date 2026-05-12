using System.IO.Compression;
using System.Text.Json;
using Ginkgo.Domain;
using Ginkgo.Domain.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Ginkgo.Api.Modules;

public sealed record ModuleRepoItem(ModuleManifest Manifest, string PackagePath);

public sealed class InstalledModulesStore
{
    private readonly List<InstalledModule> _installed = new();
    private readonly IServiceProvider? _services;
    public InstalledModulesStore() { }
    public InstalledModulesStore(IServiceProvider services) { _services = services; }
    public IReadOnlyList<InstalledModule> List() => _installed;
    public async Task AddOrUpdateAsync(InstalledModule m)
    {
        var idx = _installed.FindIndex(x => string.Equals(x.Id, m.Id, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) _installed[idx] = m; else _installed.Add(m);
        try
        {
            if (_services != null)
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SqlSugar.ISqlSugarClient>();

                // 直接查询（不过滤软删除），以便重新激活已删除的模块
                var exists = await db.Queryable<Ginkgo.Domain.Modules.InstalledModuleEntity>()
                    .Where(x => x.ModuleId == m.Id)
                    .FirstAsync();

                if (exists == null)
                {
                    // 新增记录
                    var newEntity = new Ginkgo.Domain.Modules.InstalledModuleEntity
                    {
                        Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId(),
                        ModuleId = m.Id,
                        Name = m.Name,
                        Version = m.Version,
                        HasClient = m.HasClient,
                        Enabled = m.Enabled,
                        Publisher = m.Publisher,
                        Homepage = m.Homepage,
                        InstalledAtUtc = m.InstalledAtUtc,
                        MenuRootCode = m.MenuRootCode,
                        CreatedAt = DateTime.Now,
                        IsDeleted = false
                    };
                    await db.Insertable(newEntity).ExecuteCommandAsync();
                }
                else
                {
                    // 更新现有记录（包括重新激活已删除的）
                    exists.Name = m.Name;
                    exists.Version = m.Version;
                    exists.HasClient = m.HasClient;
                    exists.Enabled = m.Enabled;
                    exists.Publisher = m.Publisher;
                    exists.Homepage = m.Homepage;
                    exists.InstalledAtUtc = m.InstalledAtUtc;
                    // 仅当传入值非空时才覆盖，避免启动恢复时误清已持久化的菜单根编码
                    if (!string.IsNullOrWhiteSpace(m.MenuRootCode))
                        exists.MenuRootCode = m.MenuRootCode;
                    exists.IsDeleted = false;
                    exists.UpdatedAt = DateTime.Now;
                    await db.Updateable(exists).ExecuteCommandAsync();
                }
            }
        }
        catch { }
    }

    public void Remove(string moduleId)
    {
        var idx = _installed.FindIndex(x => string.Equals(x.Id, moduleId, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) _installed.RemoveAt(idx);
        try
        {
            if (_services != null)
            {
                using var scope = _services.CreateScope();
                var moduleRepo = scope.ServiceProvider.GetRequiredService<IRepository<Ginkgo.Domain.Modules.InstalledModuleEntity>>();
                
                // 使用 SqlSugar 仓储删除（软删除）
                var allModules = moduleRepo.GetAllAsync().GetAwaiter().GetResult();
                var exists = allModules.FirstOrDefault(x => x.ModuleId == moduleId);
                if (exists != null)
                {
                    moduleRepo.DeleteAsync(exists.Id).GetAwaiter().GetResult();
                }
            }
        }
        catch { }
    }
}

public sealed class ModuleRepository
{
    private readonly IWebHostEnvironment _env;
    public ModuleRepository(IWebHostEnvironment env) { _env = env; }

    public IEnumerable<ModuleRepoItem> ScanRepo()
    {
        var repoDir = Path.Combine(_env.ContentRootPath, "modules_repo");
        if (!Directory.Exists(repoDir)) yield break;
        foreach (var pkg in Directory.EnumerateFiles(repoDir, "*.gmod.zip", SearchOption.TopDirectoryOnly))
        {
            using var zip = ZipFile.OpenRead(pkg);
            var entry = zip.Entries.FirstOrDefault(e => e.FullName.Replace('\\','/').EndsWith("module.json", StringComparison.OrdinalIgnoreCase));
            if (entry == null) continue;
            using var s = entry.Open();
            var manifest = JsonSerializer.Deserialize<ModuleManifest>(s);
            if (manifest != null) yield return new ModuleRepoItem(manifest, pkg);
        }
    }
}
