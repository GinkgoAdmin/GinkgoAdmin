using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Ginkgo.Api.Bootstrap
{
    /// <summary>
    /// 根据配置的 IncludeGroups，只对具备 ApiExplorer GroupName 且命中名单的接口，设置 Swagger Tag = GroupName；
    /// 其它接口保持原有标签/分组不变，确保对主框架非侵入、向后兼容。
    /// 配置示例：
    /// Swagger: { IncludeGroups: [ "community" ] }
    /// </summary>
    internal sealed class CommunityGroupTagOperationFilter : IOperationFilter
    {
        private readonly HashSet<string> _includeGroups;
        public CommunityGroupTagOperationFilter(IOptions<SwaggerGroupOptions> options)
        {
            _includeGroups = options.Value.IncludeGroups ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var group = context.ApiDescription.GroupName;
            if (!string.IsNullOrEmpty(group) && _includeGroups.Contains(group))
            {
                operation.Tags = new List<OpenApiTag> { new OpenApiTag { Name = group } };
            }
        }
    }
}

