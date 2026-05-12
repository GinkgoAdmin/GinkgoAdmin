using Ginkgo.Api.Modules;

namespace Ginkgo.Tests.Unit.Modules;

public sealed class PendingDeleteManagerTests
{
    [Fact]
    public void CleanupPendingDeletes_RemovesDirectoryBeforeModulePreload()
    {
        var root = Path.Combine(Path.GetTempPath(), "ginkgo-pending-delete-tests", Guid.NewGuid().ToString("N"));
        var moduleDir = Path.Combine(root, "modules", "Ginkgo.Module.Sample");
        Directory.CreateDirectory(moduleDir);
        File.WriteAllText(Path.Combine(moduleDir, "module.json"), "{}");
        File.WriteAllText(
            Path.Combine(root, "pending_delete.json"),
            System.Text.Json.JsonSerializer.Serialize(new[] { moduleDir }));

        try
        {
            PendingDeleteManager.CleanupPendingDeletes(root);

            Assert.False(Directory.Exists(moduleDir));
            Assert.Equal("[]", File.ReadAllText(Path.Combine(root, "pending_delete.json")).Replace("\r", string.Empty).Replace("\n", string.Empty).Replace(" ", string.Empty));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
