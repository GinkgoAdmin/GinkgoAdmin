// 文件功能说明：
// 标记实体为「系统共享」。在 SeparateDb 模式下不会被复制到租户库；
// 仅存于主库，所有租户共享读取。

namespace Ginkgo.Plugin.Abstractions.Tenancy;

/// <summary>
/// 标记实体为「系统共享表」：永远只存在于主库，对所有租户透明可见（如字典、菜单、模板等）。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SystemSharedAttribute : Attribute
{
    public string? Category { get; set; }

    public SystemSharedAttribute() { }

    public SystemSharedAttribute(string category)
    {
        Category = category;
    }
}
