// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）正确性属性测试 —— Property 16：默认菜单组预置幂等。
// 对应设计文档《Correctness Properties / Property 16》与任务 12.2，验证需求 4.5：
//   对于任意初始数据库状态，连续多次执行默认菜单组预置（种子）逻辑与执行一次等价：
//     - 不重复创建记录（同一 Slug 不产生重复菜单组）；
//     - 已存在的预置组（按 Slug 命中）内容保持不变；
//     - 若同端已存在其他 IsDefault=1 历史默认组（不同 Slug），则预置组不被创建（保留历史默认）。
// 关键约束：本测试不重写预置逻辑，而是通过反射直接调用主框架的真实生产代码
//   Ginkgo.Api.Bootstrap.DatabaseMaintenanceService.EnsureDefaultMenuGroups(ISqlSugarClient)（私有静态方法），
//   在 InMemoryTestDatabase 的 SQLite 内存库（db.Client）上连续执行多次，断言「多次执行 == 执行一次」。
// 测试策略（与设计《Testing Strategy》一致）：xUnit + FsCheck.Xunit，单个属性测试、最少 100 次迭代，
//   不 mock SqlSugar，不自行实现属性测试框架。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FsCheck;
using FsCheck.Xunit;
using Ginkgo.Api.Bootstrap;
using Ginkgo.Application.Tests.Infrastructure;
using Ginkgo.Domain.Menus;
using Ginkgo.Domain.Utils;

namespace Ginkgo.Application.Tests.Properties;

/// <summary>
/// 默认菜单组预置幂等属性测试（Property 16）。
/// </summary>
public sealed class DefaultMenuGroupIdempotencyProperty
{
    /// <summary>
    /// 三端默认菜单组预置定义（与生产代码 EnsureDefaultMenuGroups 中保持一致：Slug 唯一、单一终端类型）。
    /// </summary>
    private static readonly (string Slug, string ClientType)[] Presets =
    {
        ("default-uniapp", "UNIAPP"),
        ("default-web-portal", "WEB_PORTAL"),
        ("default-wpf", "WPF"),
    };

    /// <summary>
    /// 反射定位生产代码中的私有静态预置方法，确保属性测试验证的是真实生产逻辑而非复制实现。
    /// </summary>
    private static readonly MethodInfo EnsureMethod =
        typeof(DatabaseMaintenanceService).GetMethod(
            "EnsureDefaultMenuGroups",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "未能通过反射定位 DatabaseMaintenanceService.EnsureDefaultMenuGroups(ISqlSugarClient) 方法。");

    /// <summary>
    /// 初始库中的一个菜单组种子（用于构造随机初始库状态）。
    /// </summary>
    private sealed record SeedGroup(string Name, string Slug, string ClientType, bool IsDefault, bool IsSystem);

    /// <summary>
    /// 菜单组关键字段快照：用于「多次执行 == 执行一次」与「已存在预置组内容不变」的结构化比较。
    /// </summary>
    private static (long Id, string Slug, string Name, string? ClientType, bool IsDefault, bool IsSystem)
        Snapshot(MenuGroup g)
        => (g.Id, g.Slug, g.Name, g.ClientType, g.IsDefault, g.IsSystem);

    /// <summary>
    /// 通过反射调用真实生产预置逻辑；将反射包装异常解包为内部真实异常，便于失败时定位。
    /// </summary>
    private static void InvokeEnsure(InMemoryTestDatabase db)
    {
        try
        {
            EnsureMethod.Invoke(null, new object[] { db.Client });
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    /// <summary>
    /// 生成某个预置（Slug/ClientType）对应的初始种子集合：
    /// - 以一定概率预置「同名 Slug 的历史组」（字段随机，用于验证按 Slug 命中后内容不变）；
    /// - 以一定概率预置「同端不同 Slug 的历史默认组（IsDefault=1）」（用于验证保留历史默认、跳过创建预置组）。
    /// 历史组的 Slug 用雪花 Id 保证全局唯一，避免初始库自身出现重复 Slug。
    /// </summary>
    private static Gen<List<SeedGroup>> PresetSeedGen((string Slug, string ClientType) preset)
    {
        return from includePresetSlug in PortalGenerators.Bool()
               from presetName in PortalGenerators.Title()
               from presetIsDefault in PortalGenerators.Bool()
               from presetIsSystem in PortalGenerators.Bool()
               from includeHistoricalDefault in PortalGenerators.Bool()
               from histName in PortalGenerators.Title()
               from histIsSystem in PortalGenerators.Bool()
               select BuildPresetSeeds(
                   preset, includePresetSlug, presetName, presetIsDefault, presetIsSystem,
                   includeHistoricalDefault, histName, histIsSystem);
    }

    private static List<SeedGroup> BuildPresetSeeds(
        (string Slug, string ClientType) preset,
        bool includePresetSlug, string presetName, bool presetIsDefault, bool presetIsSystem,
        bool includeHistoricalDefault, string histName, bool histIsSystem)
    {
        var seeds = new List<SeedGroup>();
        if (includePresetSlug)
        {
            // 已存在「相同 Slug」的历史组：字段刻意随机，用于验证预置逻辑按 Slug 命中后不修改其内容。
            seeds.Add(new SeedGroup(presetName, preset.Slug, preset.ClientType, presetIsDefault, presetIsSystem));
        }
        if (includeHistoricalDefault)
        {
            // 同端、不同 Slug 的历史默认组（IsDefault=1）：用于验证保留历史默认、跳过创建预置 Slug。
            var histSlug = "hist-" + SnowflakeIdGenerator.NextId();
            seeds.Add(new SeedGroup(histName, histSlug, preset.ClientType, true, histIsSystem));
        }
        return seeds;
    }

    /// <summary>
    /// 生成与三端预置无关的「干扰组」集合（其他终端类型、不同 Slug），用于增加初始库多样性。
    /// 使用 WEB_ADMIN 终端类型（不在预置范围内），确保不影响预置规则的判定。
    /// </summary>
    private static Gen<List<SeedGroup>> UnrelatedSeedGen()
    {
        return Gen.Choose(0, 3).SelectMany(count =>
            count == 0
                ? Gen.Constant(new List<SeedGroup>())
                : Gen.Sequence(Enumerable.Range(0, count).Select(_ => UnrelatedOneGen()))
                    .Select(seq => seq.ToList()));
    }

    private static Gen<SeedGroup> UnrelatedOneGen()
    {
        return from name in PortalGenerators.Title()
               from isDefault in PortalGenerators.Bool()
               from isSystem in PortalGenerators.Bool()
               select new SeedGroup(name, "other-" + SnowflakeIdGenerator.NextId(), "WEB_ADMIN", isDefault, isSystem);
    }

    /// <summary>
    /// 组合生成随机初始库状态：覆盖空库、含部分预置 Slug、含同端历史默认组、含无关组等组合。
    /// </summary>
    private static Gen<List<SeedGroup>> SeedListGen()
    {
        return PresetSeedGen(Presets[0]).SelectMany(a =>
            PresetSeedGen(Presets[1]).SelectMany(b =>
                PresetSeedGen(Presets[2]).SelectMany(c =>
                    UnrelatedSeedGen().Select(d =>
                        a.Concat(b).Concat(c).Concat(d).ToList()))));
    }

    private static MenuGroup BuildGroup(SeedGroup seed)
    {
        return MenuGroup.Create(
            name: seed.Name,
            slug: seed.Slug,
            clientType: seed.ClientType,
            isSystem: seed.IsSystem,
            isDefault: seed.IsDefault);
    }

    // Feature: multi-client-plugin-portal, Property 16: 默认菜单组预置幂等
    /// <summary>
    /// Property 16：对于任意初始库状态，连续多次执行默认菜单组预置逻辑与执行一次等价：
    /// 不重复创建（无重复 Slug）、首次执行后的菜单组集合与末次执行后的集合完全一致，
    /// 且已存在的预置组（按 Slug 命中）内容不变、同端已有历史默认组时不创建预置组。
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Property(MaxTest = PortalPropertyConfig.MaxTest)]
    public void DefaultMenuGroupPreset_Should_Be_Idempotent()
    {
        Prop.ForAll(SeedListGen().ToArbitrary(), seeds =>
        {
            // 每次迭代使用独立内存库，避免跨迭代数据污染。
            using var db = new InMemoryTestDatabase();
            var groupRepo = new InMemoryRepository<MenuGroup>(db);

            // 1. 播种随机初始库状态。
            if (seeds.Count > 0)
            {
                var entities = seeds.Select(BuildGroup).ToList();
                groupRepo.AddRangeAsync(entities).GetAwaiter().GetResult();
            }

            // 2. 预置组已存在的 Slug 集合（命中后应内容不变），记录其初始字段快照。
            var presetSlugs = Presets.Select(p => p.Slug).ToHashSet(StringComparer.Ordinal);
            var preExistingPresetBefore = groupRepo.Query().ToList()
                .Where(g => presetSlugs.Contains(g.Slug))
                .ToDictionary(g => g.Slug, Snapshot, StringComparer.Ordinal);

            // 3. 连续多次执行真实预置逻辑（首次可能创建，其后应为幂等空操作）。
            InvokeEnsure(db);
            var afterFirst = groupRepo.Query().ToList()
                .Select(Snapshot).OrderBy(t => t.Slug, StringComparer.Ordinal).ThenBy(t => t.Id).ToList();

            InvokeEnsure(db);
            InvokeEnsure(db);
            var afterLast = groupRepo.Query().ToList()
                .Select(Snapshot).OrderBy(t => t.Slug, StringComparer.Ordinal).ThenBy(t => t.Id).ToList();

            // 断言 1（幂等核心）：首次执行后的集合与末次执行后的集合完全一致（多次执行 == 执行一次，不重复创建）。
            if (afterFirst.Count != afterLast.Count) return false;
            for (var i = 0; i < afterFirst.Count; i++)
            {
                if (!afterFirst[i].Equals(afterLast[i])) return false;
            }

            var finalGroups = groupRepo.Query().ToList();

            // 断言 2：不存在重复 Slug（每个 Slug 恰对应一条菜单组）。
            var hasDuplicateSlug = finalGroups
                .GroupBy(g => g.Slug, StringComparer.Ordinal)
                .Any(grp => grp.Count() > 1);
            if (hasDuplicateSlug) return false;

            // 断言 3：初始已存在的预置 Slug 组，其内容（Id/Name/ClientType/IsDefault/IsSystem）保持不变。
            foreach (var kv in preExistingPresetBefore)
            {
                var after = finalGroups.SingleOrDefault(g => string.Equals(g.Slug, kv.Key, StringComparison.Ordinal));
                if (after is null) return false;
                if (!Snapshot(after).Equals(kv.Value)) return false;
            }

            // 断言 4：按预置规则逐端校验创建/跳过结果（依据初始状态推导期望，验证生产逻辑符合规格）。
            foreach (var (slug, clientType) in Presets)
            {
                var presetSlugSeeded = seeds.Any(s => string.Equals(s.Slug, slug, StringComparison.Ordinal));
                var hasHistoricalDefault = seeds.Any(s =>
                    s.IsDefault && string.Equals(s.ClientType, clientType, StringComparison.Ordinal));
                var presetExistsAfter = finalGroups.Any(g => string.Equals(g.Slug, slug, StringComparison.Ordinal));

                if (presetSlugSeeded)
                {
                    // 规则 1：Slug 已存在 → 保留既有，预置 Slug 仍存在（内容由断言 3 校验）。
                    if (!presetExistsAfter) return false;
                }
                else if (hasHistoricalDefault)
                {
                    // 规则 2：同端已有其他 IsDefault=1 历史默认组 → 跳过创建预置 Slug。
                    if (presetExistsAfter) return false;
                }
                else
                {
                    // 既无同 Slug 也无同端历史默认 → 预置组应被创建（IsDefault=1、IsSystem=1、单一终端类型）。
                    var created = finalGroups.SingleOrDefault(g =>
                        string.Equals(g.Slug, slug, StringComparison.Ordinal));
                    if (created is null) return false;
                    if (!created.IsDefault || !created.IsSystem) return false;
                    if (!string.Equals(created.ClientType, clientType, StringComparison.Ordinal)) return false;
                }
            }

            return true;
        }).QuickCheckThrowOnFailure();
    }
}
