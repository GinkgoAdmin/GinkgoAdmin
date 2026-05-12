// 文件功能说明：
// 领域事件处理器：用户注册完成后，读取 ginkgo_Sys_Settings 中的 Registration.* 配置，
// 初始化用户的默认角色与默认部门；遵循 DDD（事件 -> 应用层处理器 -> 领域仓储）。

using Ginkgo.Domain.Events;
using Ginkgo.Domain.Users.Events;
using Ginkgo.Domain.Users;
using Ginkgo.Domain.Settings;

namespace Ginkgo.Application.Users.Handlers;

public sealed class UserRegisteredHandler : IDomainEventHandler<UserRegistered>
{
    private readonly IUserRoleRepository _userRoleRepo;
    private readonly IUserDepartmentRepository _userDeptRepo;
    private readonly ISettingsRepository _settingsRepo;

    public UserRegisteredHandler(
        IUserRoleRepository userRoleRepo,
        IUserDepartmentRepository userDeptRepo,
        ISettingsRepository settingsRepo)
    {
        _userRoleRepo = userRoleRepo;
        _userDeptRepo = userDeptRepo;
        _settingsRepo = settingsRepo;
    }

    public async Task HandleAsync(UserRegistered notification, CancellationToken cancellationToken)
    {
        // 读取默认角色 Id 列表：Registration.DefaultRoleIds（字符串或 JSON 数组均可）
        var roleSetting = await _settingsRepo.GetAsync("Registration.DefaultRoleIds", null, cancellationToken);
        var roleIds = ParseLongArray(roleSetting?.Value);
        if (roleIds.Count > 0)
        {
            await _userRoleRepo.ReplaceAsync(notification.UserId, roleIds, cancellationToken);
        }

        // 读取默认部门 Id：Registration.DefaultDepartmentId
        var deptSetting = await _settingsRepo.GetAsync("Registration.DefaultDepartmentId", null, cancellationToken);
        var deptId = ParseLong(deptSetting?.Value);
        if (deptId.HasValue)
        {
            await _userDeptRepo.ReplaceAsync(notification.UserId, new[] { deptId.Value }, cancellationToken);
        }
    }

    private static List<long> ParseLongArray(string? text)
    {
        var result = new List<long>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        // 允许两种格式：以逗号分隔的 long 字符串；或 JSON 数组 ["123","456"]
        var t = text.Trim();
        try
        {
            if (t.StartsWith("["))
            {
                var arr = System.Text.Json.JsonSerializer.Deserialize<string[]>(t);
                if (arr != null)
                {
                    foreach (var s in arr)
                    {
                        if (long.TryParse(s, out var g) && g != 0) result.Add(g);
                    }
                }
                return result.Distinct().ToList();
            }

            // CSV
            foreach (var s in t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (long.TryParse(s, out var g) && g != 0) result.Add(g);
            }
            return result.Distinct().ToList();
        }
        catch
        {
            return new List<long>();
        }
    }

    private static long? ParseLong(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (long.TryParse(text.Trim(), out var g) && g != 0) return g;
        return null;
    }
}


