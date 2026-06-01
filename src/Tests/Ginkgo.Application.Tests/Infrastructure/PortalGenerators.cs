// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）属性测试用的 FsCheck 生成器集合。
// 依据设计文档《Testing Strategy / 属性测试配置要求》，生成器需覆盖以下边界：
//   - 空集合；
//   - 单端 / 多端（逗号分隔）ClientType；
//   - RequireGrant 0/1；
//   - Enabled 0/1；
//   - 超管 / 非超管 / 无角色用户；
//   - 重复 path（同一入口标识重复）；
//   - 混合 Module（含 'sys'）；
//   - 乱序 Order；
//   - 父子 / 孤儿 ParentId。
// 生成器只负责产出领域实体与输入数据，不直接写库；由具体属性测试将其灌入内存仓储。

using System.Collections.Generic;
using System.Linq;
using FsCheck;
using Ginkgo.Domain.Menus;
using Ginkgo.Domain.Roles;
using Ginkgo.Domain.Users;
using Ginkgo.Domain.Utils;

namespace Ginkgo.Application.Tests.Infrastructure;

/// <summary>
/// 本特性属性测试的统一生成器入口。
/// </summary>
public static class PortalGenerators
{
    // ===== 基础标量生成器 =====

    /// <summary>布尔值生成器（覆盖 0/1，用于 IsDefault / RequireGrant / Enabled）。</summary>
    public static Gen<bool> Bool() => Gen.Elements(true, false);

    /// <summary>单一终端类型生成器（UNIAPP / WEB_PORTAL / WPF）。</summary>
    public static Gen<string> SingleClientType() => Gen.Elements(PortalClientTypes.Single);

    /// <summary>
    /// 多端（逗号分隔）终端类型生成器：从单端集合中取 2~3 个去重拼接，
    /// 用于校验「默认标识要求单一终端类型」的拒绝路径。
    /// </summary>
    public static Gen<string> MultiClientType() =>
        Gen.Choose(2, PortalClientTypes.Single.Length)
            .Select(n => string.Join(",", PortalClientTypes.Single.Take(n)));

    /// <summary>任意 ClientType 生成器：单端或多端皆可能。</summary>
    public static Gen<string> AnyClientType() =>
        Gen.OneOf(SingleClientType(), MultiClientType());

    /// <summary>
    /// 模块归属生成器：覆盖主框架 'sys' 与若干插件 ModuleId（区分大小写）。
    /// </summary>
    public static Gen<string> Module() => Gen.Elements(
        "sys",
        "Ginkgo.Module.SmartCommunity",
        "Ginkgo.Module.Evaluate",
        "Ginkgo.Module.Demo");

    /// <summary>排序号生成器（含负数与乱序场景）。</summary>
    public static Gen<int> OrderNo() => Gen.Choose(-5, 20);

    /// <summary>
    /// 入口路径（path）生成器：从一个小集合取值，
    /// 以提高「重复 path」出现概率，覆盖 upsert 幂等与唯一标识场景。
    /// </summary>
    public static Gen<string> Path() => Gen.Elements(
        "/pages/plugins/demo/index",
        "/pages/plugins/demo/list",
        "/pages/plugins/demo/event-handle",
        "/pages/plugins/demo/center");

    /// <summary>图标生成器（含 null）。</summary>
    public static Gen<string?> Icon() => Gen.Elements<string?>(
        "ri-home-line", "ri-community-line", "ri-mic-line", null);

    /// <summary>角标生成器（含 null）。</summary>
    public static Gen<string?> Badge() => Gen.Elements<string?>("New", "Hot", "99+", null);

    /// <summary>标题生成器（简体中文，非空）。</summary>
    public static Gen<string> Title() => Gen.Elements("事件办理", "智慧社区", "评估中心", "数据看板", "我的待办");

    // ===== 菜单组生成器 =====

    /// <summary>
    /// 生成单个菜单组：随机 ClientType（单端/多端）、IsDefault、Enabled、IsSystem。
    /// </summary>
    public static Gen<MenuGroup> MenuGroupGen()
    {
        return from clientType in AnyClientType()
               from isDefault in Bool()
               from enabled in Bool()
               from isSystem in Bool()
               select BuildMenuGroup(clientType, isDefault, enabled, isSystem);
    }

    /// <summary>
    /// 生成「单端默认组」的菜单组：ClientType 为单一终端、IsDefault=1、Enabled=1。
    /// 用于 portal / 注入相关属性测试构造默认组。
    /// </summary>
    public static Gen<MenuGroup> DefaultMenuGroupGen()
    {
        return from clientType in SingleClientType()
               select BuildMenuGroup(clientType, isDefault: true, enabled: true, isSystem: true);
    }

    /// <summary>
    /// 生成菜单组集合（含空集合）：0~6 个随机菜单组。
    /// </summary>
    public static Gen<List<MenuGroup>> MenuGroupListGen()
    {
        return Gen.Choose(0, 6).SelectMany(count =>
            count == 0
                ? Gen.Constant(new List<MenuGroup>())
                : Gen.Sequence(Enumerable.Range(0, count).Select(_ => MenuGroupGen()))
                    .Select(seq => seq.ToList()));
    }

    private static MenuGroup BuildMenuGroup(string? clientType, bool isDefault, bool enabled, bool isSystem)
    {
        var id = SnowflakeIdGenerator.NextId();
        var group = MenuGroup.Create(
            name: "菜单组-" + id,
            slug: "group-" + id,
            clientType: clientType,
            isSystem: isSystem,
            isDefault: isDefault);
        if (enabled) group.Enable(); else group.Disable();
        return group;
    }

    // ===== 菜单组项生成器 =====

    /// <summary>
    /// 生成隶属于指定菜单组的单个菜单组项：随机 Module、RequireGrant、Enabled、Order、path。
    /// ParentId 暂置空，由列表生成器统一处理父子/孤儿关系。
    /// </summary>
    public static Gen<MenuGroupItem> MenuGroupItemGen(long menuGroupId)
    {
        return from title in Title()
               from module in Module()
               from requireGrant in Bool()
               from enabled in Bool()
               from order in OrderNo()
               from url in Path()
               from icon in Icon()
               from badge in Badge()
               select BuildItem(menuGroupId, title, module, requireGrant, enabled, order, url, icon, badge, parentId: null);
    }

    /// <summary>
    /// 生成隶属于指定菜单组的菜单组项集合，并构造父子/孤儿 ParentId 与乱序 Order。
    /// - 约 1/3 概率为孤儿（ParentId 指向集合外的随机 Id）；
    /// - 其余在已生成项中选取父节点形成树形；
    /// - 第一个项始终为根（ParentId=null）。
    /// </summary>
    public static Gen<List<MenuGroupItem>> MenuGroupItemListGen(long menuGroupId)
    {
        return Gen.Choose(0, 8).SelectMany(count =>
        {
            if (count == 0) return Gen.Constant(new List<MenuGroupItem>());
            return Gen.Sequence(Enumerable.Range(0, count).Select(_ => MenuGroupItemGen(menuGroupId)))
                .SelectMany(seq =>
                {
                    var items = seq.ToList();
                    // 为每个非首项生成父子/孤儿关系决策。
                    return Gen.Sequence(items.Select((_, idx) => ParentDecisionGen(idx)))
                        .Select(decisions =>
                        {
                            var decisionList = decisions.ToList();
                            for (int i = 0; i < items.Count; i++)
                            {
                                if (i == 0)
                                {
                                    items[i].MoveTo(null); // 根节点
                                    continue;
                                }
                                var (isOrphan, parentPick) = decisionList[i];
                                if (isOrphan)
                                {
                                    // 孤儿：ParentId 指向集合中不存在的随机 Id
                                    items[i].MoveTo(SnowflakeIdGenerator.NextId());
                                }
                                else
                                {
                                    var parentIndex = parentPick % i; // 取前序项作为父，保证非自引用
                                    items[i].MoveTo(items[parentIndex].Id);
                                }
                            }
                            return items;
                        });
                });
        });
    }

    private static Gen<(bool isOrphan, int parentPick)> ParentDecisionGen(int index)
    {
        return from orphanRoll in Gen.Choose(0, 2) // 1/3 概率孤儿
               from parentPick in Gen.Choose(0, 1000)
               select (orphanRoll == 0, parentPick);
    }

    private static MenuGroupItem BuildItem(long menuGroupId, string title, string module,
        bool requireGrant, bool enabled, int order, string url, string? icon, string? badge, long? parentId)
    {
        var item = MenuGroupItem.Create(
            menuGroupId: menuGroupId,
            title: title,
            linkType: "Custom",
            url: url,
            parentId: parentId,
            module: module,
            requireGrant: requireGrant);
        item.Icon = icon;
        item.Badge = badge;
        item.SetOrder(order);
        if (enabled) item.Enable(); else item.Disable();
        return item;
    }

    // ===== 客户端入口声明生成器 =====

    /// <summary>
    /// 生成单条客户端入口声明项（与 install.json items 同形）。
    /// </summary>
    public static Gen<ClientMenuItemSpec> ClientMenuItemSpecGen()
    {
        return from title in Title()
               from icon in Icon()
               from path in Path()
               from requireGrant in Bool()
               from order in OrderNo()
               from badge in Badge()
               select new ClientMenuItemSpec(title, icon, path, requireGrant, order, badge);
    }

    /// <summary>
    /// 生成客户端入口声明项集合（含空集合与重复 path）。
    /// </summary>
    public static Gen<List<ClientMenuItemSpec>> ClientMenuItemSpecListGen()
    {
        return Gen.Choose(0, 6).SelectMany(count =>
            count == 0
                ? Gen.Constant(new List<ClientMenuItemSpec>())
                : Gen.Sequence(Enumerable.Range(0, count).Select(_ => ClientMenuItemSpecGen()))
                    .Select(seq => seq.ToList()));
    }

    /// <summary>
    /// 生成一条完整的客户端入口声明（随机单端 ClientType + 一组入口项）。
    /// </summary>
    public static Gen<ClientMenusSpec> ClientMenusSpecGen()
    {
        return from clientType in SingleClientType()
               from items in ClientMenuItemSpecListGen()
               select new ClientMenusSpec(clientType, items);
    }

    // ===== 用户 / 角色场景生成器 =====

    /// <summary>
    /// 生成用户角色场景：覆盖超管、非超管（持角色）、无角色三种情形。
    /// </summary>
    public static Gen<UserRoleScenario> UserRoleScenarioGen(IReadOnlyList<long> candidateRoleIds)
    {
        // 三类：0=无角色，1=非超管持角色，2=超管
        return Gen.Choose(0, 2).SelectMany(kind =>
        {
            var userId = SnowflakeIdGenerator.NextId();
            if (kind == 0 || candidateRoleIds.Count == 0)
            {
                return Gen.Constant(new UserRoleScenario(userId, new List<long>(), false));
            }
            if (kind == 2)
            {
                // 超管：至少持有一个角色，标记为超管
                return PickRoleIds(candidateRoleIds)
                    .Select(roles => new UserRoleScenario(userId,
                        roles.Count == 0 ? new List<long> { candidateRoleIds[0] } : roles, true));
            }
            // 非超管：持有 0~N 个角色
            return PickRoleIds(candidateRoleIds)
                .Select(roles => new UserRoleScenario(userId, roles, false));
        });
    }

    private static Gen<List<long>> PickRoleIds(IReadOnlyList<long> candidateRoleIds)
    {
        return Gen.Choose(0, candidateRoleIds.Count).SelectMany(take =>
            Gen.Constant(candidateRoleIds.Take(take).Distinct().ToList()));
    }

    // ===== 角色实体生成器 =====

    /// <summary>
    /// 生成角色实体（随机超管标记）。
    /// </summary>
    public static Gen<Role> RoleGen()
    {
        return from isSuper in Bool()
               select BuildRole(isSuper);
    }

    private static Role BuildRole(bool isSuperAdmin)
    {
        var id = SnowflakeIdGenerator.NextId();
        return new Role
        {
            Id = id,
            Name = "角色-" + id,
            Code = "role-" + id,
            Enabled = true,
            IsSuperAdmin = isSuperAdmin
        };
    }

    /// <summary>
    /// 将场景中的用户角色关系转换为 UserRole 实体集合，便于写入内存仓储。
    /// </summary>
    public static List<UserRole> ToUserRoles(UserRoleScenario scenario)
    {
        return scenario.RoleIds.Select(roleId => new UserRole
        {
            Id = SnowflakeIdGenerator.NextId(),
            UserId = scenario.UserId,
            RoleId = roleId
        }).ToList();
    }
}
