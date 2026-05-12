using System;
using System.Collections.Generic;

namespace Ginkgo.Api.Bootstrap
{
    /// <summary>
    /// Swagger 分组加载配置。
    /// 通过 IncludeGroups 指定哪些 ApiExplorer GroupName 需要并入当前文档并以该组名作为 Tag 显示。
    /// 例如：Swagger:IncludeGroups = [ "community" ]。
    /// </summary>
    internal sealed class SwaggerGroupOptions
    {
        public HashSet<string> IncludeGroups { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}

