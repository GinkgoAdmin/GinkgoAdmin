// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）正确性属性测试 —— Property 15：雪花 Id 对前端序列化为字符串。
//
// 设计依据（design.md《Correctness Properties / Property 15》、需求 9.10 与 14.3）：
//   对任意 /client/portal 返回的入口树，输出中所有雪花 Id 字段（如 id、parentId、groupId）均序列化为字符串形式。
//
// 关键说明（如何复用「真实全局序列化契约」）：
//   ClientPortalDto / ClientPortalItemDto 的雪花 Id 字段在 C# 侧均为 long / long?，
//   System.Text.Json 默认会把它们写成 JSON 数字（number）。框架之所以最终对前端输出字符串，
//   是因为主框架在 Ginkgo.Api/Bootstrap/ServiceRegistration.cs 的 AddJsonOptions 中全局注册了：
//     - System.Text.Json.JsonNamingPolicy.CamelCase（属性名 camelCase）
//     - Ginkgo.Api.Filters.LongToStringConverter        （long  → 字符串）
//     - Ginkgo.Api.Filters.NullableLongToStringConverter（long? → 字符串 / null）
//     - DateTimeToUtcConverter / NullableDateTimeToUtcConverter（与本属性无关，但一并对齐契约）
//   本测试不另写一份「行为相近」的转换器，而是直接取用主框架同一份转换器类型构造 JsonSerializerOptions，
//   从而真正验证「框架全局序列化契约把雪花 Id 输出为字符串」这一对外约定（否则默认序列化会输出数字）。
//
// 测试框架：xUnit + FsCheck.Xunit（不自行实现属性测试框架），最少运行 100 次迭代。

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Ginkgo.Api.Filters;
using Ginkgo.Application.Menus;
using Ginkgo.Application.Tests.Infrastructure;
using Ginkgo.Domain.Utils;

namespace Ginkgo.Application.Tests.Properties;

/// <summary>
/// Property 15：雪花 Id 对前端序列化为字符串。
/// </summary>
public sealed class SnowflakeIdSerializationProperty
{
    /// <summary>
    /// 构造与主框架全局一致的 JSON 序列化选项：复用 Ginkgo.Api.Filters 中真实的 long / long? 转换器，
    /// 使本测试验证的是「框架对外真实序列化契约」而非测试自造的等价实现。
    /// </summary>
    private static JsonSerializerOptions BuildGlobalJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new LongToStringConverter());
        options.Converters.Add(new NullableLongToStringConverter());
        options.Converters.Add(new DateTimeToUtcConverter());
        options.Converters.Add(new NullableDateTimeToUtcConverter());
        return options;
    }

    // Feature: multi-client-plugin-portal, Property 15: 雪花 Id 对前端序列化为字符串
    /// <summary>
    /// 对任意随机生成的客户端入口树（ClientPortalDto，含 GroupId 与嵌套 ClientPortalItemDto 的
    /// Id / ParentId / Children），经「框架全局 JSON 序列化契约」序列化后，断言：
    ///   - groupId 字段为 JSON 字符串（非空时）或 JSON null（无默认组时）；
    ///   - 递归遍历 items 及其 children，每个节点的 id 字段均为 JSON 字符串；
    ///   - 每个节点的 parentId 字段为 JSON 字符串（非空时）或 JSON null（根节点时）。
    /// 即所有雪花 Id 字段一律以字符串形式输出，绝不出现 JSON 数字。
    /// </summary>
    [Property(MaxTest = PortalPropertyConfig.MaxTest)]
    public void Portal_Snowflake_Ids_Should_Serialize_As_Strings()
    {
        var options = BuildGlobalJsonOptions();

        Prop.ForAll(ClientPortalDtoGen().ToArbitrary(), portal =>
        {
            var json = JsonSerializer.Serialize(portal, options);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 顶层 groupId：非空必须为字符串，空（无默认组）必须为 null。
            if (!AssertSnowflakeIdField(root, "groupId"))
            {
                return false;
            }

            // 递归校验 items 树中每个节点的 id / parentId。
            return root.TryGetProperty("items", out var items)
                   && items.ValueKind == JsonValueKind.Array
                   && items.EnumerateArray().All(AssertItemNode);
        }).QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// 递归断言单个入口项节点：id 必为字符串，parentId 为字符串或 null，且其 children 同样满足。
    /// </summary>
    private static bool AssertItemNode(JsonElement node)
    {
        // id 必须存在且为字符串（不允许 null，因雪花 Id 主键恒有值）。
        if (!node.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        // parentId 必须为字符串（非根）或 null（根节点）。
        if (!AssertSnowflakeIdField(node, "parentId"))
        {
            return false;
        }

        // children 可能不存在 / 为 null / 为数组；存在数组时递归校验。
        if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            return children.EnumerateArray().All(AssertItemNode);
        }

        return true;
    }

    /// <summary>
    /// 断言某个雪花 Id 字段：要么是 JSON 字符串（非空雪花 Id），要么是 JSON null（可空字段未赋值）。
    /// 任何 JSON 数字（Number）都视为违反「序列化为字符串」契约而失败。
    /// </summary>
    private static bool AssertSnowflakeIdField(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var el))
        {
            // 字段缺失：对可空字段而言可接受（等价于 null 语义）。
            return true;
        }

        return el.ValueKind == JsonValueKind.String || el.ValueKind == JsonValueKind.Null;
    }

    // ===== 生成器：随机客户端入口树 =====

    /// <summary>
    /// 生成随机 ClientPortalDto：随机 GroupId（含 null）、随机一组带父子嵌套的入口项（含空集合）。
    /// </summary>
    private static Gen<ClientPortalDto> ClientPortalDtoGen()
    {
        return from clientType in PortalGenerators.SingleClientType()
               from hasGroup in PortalGenerators.Bool()
               from items in PortalItemForestGen()
               select new ClientPortalDto
               {
                   ClientType = clientType,
                   GroupId = hasGroup ? SnowflakeIdGenerator.NextId() : (long?)null,
                   Items = items
               };
    }

    /// <summary>
    /// 生成入口项森林（0~4 个根节点，每个根节点可能带嵌套子节点），覆盖空集合与多层嵌套。
    /// </summary>
    private static Gen<List<ClientPortalItemDto>> PortalItemForestGen()
    {
        return Gen.Choose(0, 4).SelectMany(rootCount =>
            rootCount == 0
                ? Gen.Constant(new List<ClientPortalItemDto>())
                : Gen.Sequence(Enumerable.Range(0, rootCount)
                        .Select(_ => PortalItemNodeGen(parentId: null, depth: 0)))
                    .Select(seq => seq.ToList()));
    }

    /// <summary>
    /// 生成单个入口项节点（递归，最多 2 层子节点）：
    ///   - Id 恒为非空雪花 Id；
    ///   - ParentId 为传入父 Id（根节点为 null）；
    ///   - 随机 RequireGrant / Order / Icon / Badge / Module；
    ///   - 子节点 ParentId 指向本节点 Id，形成合法父子关系。
    /// </summary>
    private static Gen<ClientPortalItemDto> PortalItemNodeGen(long? parentId, int depth)
    {
        // 以「常量生成器 + 投影」保证每次采样都取到全新的雪花 Id（NextId 全局自增、不重复），
        // 而非在构建生成器时计算一次后被所有采样复用。
        return Gen.Constant(0L).Select(_ => SnowflakeIdGenerator.NextId()).SelectMany(id =>
            from title in PortalGenerators.Title()
            from icon in PortalGenerators.Icon()
            from url in PortalGenerators.Path()
            from badge in PortalGenerators.Badge()
            from requireGrant in PortalGenerators.Bool()
            from order in PortalGenerators.OrderNo()
            from module in PortalGenerators.Module()
            from childCount in (depth >= 2 ? Gen.Constant(0) : Gen.Choose(0, 2))
            from children in (childCount == 0
                ? Gen.Constant(new List<ClientPortalItemDto>())
                : Gen.Sequence(Enumerable.Range(0, childCount)
                        .Select(_ => PortalItemNodeGen(parentId: id, depth: depth + 1)))
                    .Select(seq => seq.ToList()))
            select new ClientPortalItemDto
            {
                Id = id,
                ParentId = parentId,
                Title = title,
                Icon = icon,
                Url = url,
                Badge = badge,
                BadgeType = badge == null ? null : "primary",
                Order = order,
                RequireGrant = requireGrant,
                Module = module,
                Children = children.Count == 0 ? null : children
            });
    }
}
