using Ginkgo.Api.Bootstrap;
using Ginkgo.Domain.Modules;
using Ginkgo.Domain.Utils;

namespace Ginkgo.Tests.Unit.Modules;

public sealed class ModuleStartupSyncPolicyTests
{
    public ModuleStartupSyncPolicyTests()
    {
        if (!SnowflakeIdGenerator.IsInitialized)
        {
            SnowflakeIdGenerator.Initialize(1);
        }
    }

    [Fact]
    public void ShouldSynchronize_SoftDeletedModule_ReturnsFalse()
    {
        var records = new[]
        {
            new InstalledModuleEntity
            {
                ModuleId = "Ginkgo.Module.AICore",
                Name = "Ginkgo.Module.AICore",
                Version = "1.0.0.0",
                IsDeleted = true,
                Enabled = true
            }
        };

        var policy = ModuleStartupSyncPolicy.FromDatabase(records);

        Assert.False(policy.ShouldSynchronize("Ginkgo.Module.AICore"));
    }

    [Fact]
    public void ResolveEnabled_NotInstalledModule_DefaultsToEnabled()
    {
        var policy = ModuleStartupSyncPolicy.FromDatabase(Array.Empty<InstalledModuleEntity>());

        Assert.True(policy.ResolveEnabled("Ginkgo.Module.NewPlugin"));
    }
}
