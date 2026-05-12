using Ginkgo.Api.Modules;

namespace Ginkgo.Tests.Unit.Modules;

public class ModulePluginDirectoryResolverTests
{
    [Fact]
    public void FindPluginDirectories_ShouldReturnDefaultAndManifestMatchedDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "ginkgo-plugin-resolver-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var defaultDir = Path.Combine(root, "aicore");
            var customDir = Path.Combine(root, "custom-ai");
            var unrelatedDir = Path.Combine(root, "oss");
            Directory.CreateDirectory(defaultDir);
            Directory.CreateDirectory(customDir);
            Directory.CreateDirectory(unrelatedDir);

            File.WriteAllText(Path.Combine(customDir, "module.json"), """{"id":"Ginkgo.Module.AICore"}""");
            File.WriteAllText(Path.Combine(unrelatedDir, "module.json"), """{"id":"Ginkgo.Module.Oss"}""");

            var dirs = ModulePluginDirectoryResolver.FindPluginDirectories(root, "Ginkgo.Module.AICore");

            Assert.Contains(Path.GetFullPath(defaultDir), dirs);
            Assert.Contains(Path.GetFullPath(customDir), dirs);
            Assert.DoesNotContain(Path.GetFullPath(unrelatedDir), dirs);
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
