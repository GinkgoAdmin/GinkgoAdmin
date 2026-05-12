using Ginkgo.Domain.Notifications;
using Ginkgo.Domain.Users;
using System.Text.RegularExpressions;

namespace Ginkgo.Application.Notifications;

/// <summary>
/// 受众解析器（默认实现，扩展支持）：
/// - 支持 TargetType=0（用户ID列表）、1（角色ID列表）、2（部门ID列表）、3（表达式：ALL/* 表示所有用户）
/// - 解析为具体用户去重返回；快照字段（UserName/DeptId/RoleId）按需后续补全
/// </summary>
public sealed class DefaultAudienceResolver : IAudienceResolver
{
    private static readonly Regex Splitter = new(@"[;,\n\r\t\s]+", RegexOptions.Compiled);
    private readonly IUserRoleRepository _userRoleRepo;
    private readonly IUserDepartmentRepository _userDeptRepo;
    private readonly IUserRepository _userRepo;

    public DefaultAudienceResolver(IUserRoleRepository userRoleRepo, IUserDepartmentRepository userDeptRepo, IUserRepository userRepo)
    {
        _userRoleRepo = userRoleRepo;
        _userDeptRepo = userDeptRepo;
        _userRepo = userRepo;
    }

    public async Task<IReadOnlyList<AudienceMember>> ResolveAsync(long notifyId, IEnumerable<AudienceSeed> seeds, CancellationToken ct = default)
    {
        if (seeds == null) return Array.Empty<AudienceMember>();
        var userSet = new HashSet<long>();

        foreach (var s in seeds)
        {
            var raw = (s.TargetValue ?? string.Empty).Trim();
            switch (s.TargetType)
            {
                case 0: // 用户
                {
                    foreach (var token in Splitter.Split(raw))
                    {
                        if (long.TryParse(token, out var uid) && uid != 0)
                            userSet.Add(uid);
                    }
                    break;
                }
                case 1: // 角色 -> 用户
                {
                    var roleIds = Splitter.Split(raw).Select(t => long.TryParse(t, out var id) ? id : 0L)
                                            .Where(id => id != 0).Distinct().ToList();
                    if (roleIds.Count > 0)
                    {
                        var users = await _userRoleRepo.GetUserIdsByRoleIdsAsync(roleIds, ct);
                        foreach (var uid in users) userSet.Add(uid);
                    }
                    break;
                }
                case 2: // 部门 -> 用户
                {
                    var deptIds = Splitter.Split(raw).Select(t => long.TryParse(t, out var id) ? id : 0L)
                                            .Where(id => id != 0).Distinct().ToList();
                    if (deptIds.Count > 0)
                    {
                        var users = await _userDeptRepo.GetUserIdsByDepartmentIdsAsync(deptIds, ct);
                        foreach (var uid in users) userSet.Add(uid);
                    }
                    break;
                }
                case 3: // 表达式：ALL/* => 全量用户
                {
                    if (string.Equals(raw, "*", StringComparison.OrdinalIgnoreCase) || string.Equals(raw, "ALL", StringComparison.OrdinalIgnoreCase))
                    {
                        var page = 1; const int size = 1000;
                        while (true)
                        {
                            var (total, users) = await _userRepo.GetPagedAsync(page, size, null, ct);
                            foreach (var u in users) userSet.Add(u.Id);
                            if (users.Count < size) break;
                            page++;
                        }
                    }
                    break;
                }
                default:
                    break; // 其它表达式暂不解析
            }
        }

        var list = userSet.Select(uid => AudienceMember.Create(notifyId, uid)).ToList();
        return list;
    }
}
