// 文件功能说明：
// 用户应用服务的基础空实现，后续将填充具体业务逻辑。

using Ginkgo.Domain;
using Ginkgo.Domain.Roles;
using Ginkgo.Domain.Departments;
using Ginkgo.Domain.Users;
using Ginkgo.Shared;
using Ginkgo.ServerToolkit;
using System.Text.Json;

namespace Ginkgo.Application.Users;

/// <summary>
/// 用户应用服务实现（占位）。
/// </summary>
public sealed class UserAppService : IUserAppService
{
    private readonly IRepository<User> _userRepoBasic;
    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IUserDepartmentRepository _userDepartmentRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRepository<Role> _roleRepoBasic;
    private readonly IRepository<Department> _deptRepoBasic;

    private readonly ICurrentUser _currentUser;

    private readonly Ginkgo.ServerToolkit.ISecondaryVerificationService _secondFactor;
    private readonly Ginkgo.Domain.Events.IDomainEventPublisher _bus;


    /// <summary>
    /// 构造函数。
    /// </summary>
    public UserAppService(IRepository<User> userRepoBasic,
                          IUserRepository userRepository,
                          IUserRoleRepository userRoleRepository,
                          IUserDepartmentRepository userDepartmentRepository,
                          IPasswordHasher passwordHasher,
                          IRepository<Role> roleRepoBasic,
                          IRepository<Department> deptRepoBasic,
                          ICurrentUser currentUser,
                          Ginkgo.ServerToolkit.ISecondaryVerificationService secondFactor,
                          Ginkgo.Domain.Events.IDomainEventPublisher bus)
    {
        _userRepoBasic = userRepoBasic;
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _userDepartmentRepository = userDepartmentRepository;
        _passwordHasher = passwordHasher;
        _roleRepoBasic = roleRepoBasic;
        _deptRepoBasic = deptRepoBasic;
        _currentUser = currentUser;
        _secondFactor = secondFactor;
        _bus = bus;
    }

    /// <inheritdoc />
    public async Task<PagedResult<UserListItemDto>> GetPagedAsync(PageRequest request, string? keyword, CancellationToken cancellationToken = default)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.PageSize <= 0 ? 20 : request.PageSize;
        var (total, list) = await _userRepository.GetPagedAsync(page, size, keyword, cancellationToken);

        // 基础映射（含邮箱/手机/创建时间）
        var items = list.Select(x => new UserListItemDto
        {
            Id = x.Id,
            UserName = x.UserName,
            DisplayName = x.DisplayName,
            Email = x.Email,
            Phone = x.Phone,
            CreatedAt = x.CreatedAt,
            Enabled = x.Enabled
        }).ToList();

        // 批量加载每个用户的角色/部门 Id（页内最多 size 次查询）
        var userIds = list.Select(u => u.Id).ToList();

        // 角色 Ids
        // 顺序执行，避免底层连接并发导致的 "Connection is already open" 问题
        var roleIdsByUser = new Dictionary<long, List<long>>();
        foreach (var uid in userIds)
        {
            roleIdsByUser[uid] = await _userRoleRepository.GetRoleIdsAsync(uid, cancellationToken);
        }
        var allRoleIds = roleIdsByUser.SelectMany(kv => kv.Value).Distinct().ToList();

        // 部门 Ids
        var deptIdsByUser = new Dictionary<long, List<long>>();
        foreach (var uid in userIds)
        {
            deptIdsByUser[uid] = await _userDepartmentRepository.GetDepartmentIdsAsync(uid, cancellationToken);
        }
        var allDeptIds = deptIdsByUser.SelectMany(kv => kv.Value).Distinct().ToList();

        // 字典：RoleId -> Name；DeptId -> Name
        var roleMap = allRoleIds.Count == 0
            ? new Dictionary<long, string>()
            : _roleRepoBasic.Query()
                .Where(r => allRoleIds.Contains(r.Id))
                .Select(r => new { r.Id, r.Name })
                .ToList()
                .ToDictionary(x => x.Id, x => x.Name);

        var deptMap = allDeptIds.Count == 0
            ? new Dictionary<long, string>()
            : _deptRepoBasic.Query()
                .Where(d => allDeptIds.Contains(d.Id))
                .Select(d => new { d.Id, d.Name })
                .ToList()
                .ToDictionary(x => x.Id, x => x.Name);

        // 回填名称集合
        foreach (var it in items)
        {
            var rids = roleIdsByUser.TryGetValue(it.Id, out var r) ? r : new List<long>();
            it.RoleNames = rids.Select(id => roleMap.TryGetValue(id, out var n) ? n : null)
                               .Where(n => !string.IsNullOrEmpty(n))
                               .Select(n => n!)
                               .ToList();

            var dids = deptIdsByUser.TryGetValue(it.Id, out var d) ? d : new List<long>();
            it.DepartmentNames = dids.Select(id => deptMap.TryGetValue(id, out var n) ? n : null)
                                     .Where(n => !string.IsNullOrEmpty(n))
                                     .Select(n => n!)
                                     .ToList();
        }

        return new PagedResult<UserListItemDto>
        {
            Total = total,
            Page = page,
            PageSize = size,
            Items = items
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<UserListItemDto>> SearchPagedAsync(PageRequest request, IDictionary<string, object?> filters, string? sortField, string? sortOrder, CancellationToken cancellationToken = default)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.PageSize <= 0 ? 20 : request.PageSize;

        // 初始基础查询（按关键字/启用状态/创建时间）
        DateTimeOffset? createdFrom = null;
        DateTimeOffset? createdTo = null;
        if (filters.TryGetValue("createdFrom", out var cf) && DateTimeOffset.TryParse(Convert.ToString(cf), out var cfo)) createdFrom = cfo;
        if (filters.TryGetValue("createdTo", out var ct) && DateTimeOffset.TryParse(Convert.ToString(ct), out var cto)) createdTo = cto;

        bool? enabled = null;
        if (filters.TryGetValue("enabled", out var en))
        {
            switch (en)
            {
                case bool b:
                    enabled = b; break;
                case string s when bool.TryParse(s, out var bv):
                    enabled = bv; break;
                case JsonElement je:
                    if (je.ValueKind == JsonValueKind.True || je.ValueKind == JsonValueKind.False)
                        enabled = je.GetBoolean();
                    else if (je.ValueKind == JsonValueKind.String && bool.TryParse(je.GetString(), out var jb))
                        enabled = jb;
                    else if (je.ValueKind == JsonValueKind.Number)
                        enabled = je.TryGetInt32(out var num) ? num != 0 : (bool?)null;
                    break;
            }
        }

        var f = new UserQueryFilter
        {
            Keyword = filters.TryGetValue("keyword", out var kw) ? Convert.ToString(kw) : null,
            Enabled = enabled,
            CreatedFrom = createdFrom,
            CreatedTo = createdTo,
        };

        // 先按用户主表过滤，拿到候选集合
        var (totalBase, baseList) = await _userRepository.SearchAsync(f, page, size, cancellationToken);
        var candidates = baseList.Select(u => u.Id).ToHashSet();

        // 处理关联过滤（部门、角色等）
        if (filters.TryGetValue("relations", out var relObj))
        {
            // JSON 对象形式
            if (relObj is JsonElement relJe && relJe.ValueKind == JsonValueKind.Object)
            {
                // 部门
                if (relJe.TryGetProperty("departments", out var depJe) && depJe.ValueKind == JsonValueKind.Object)
                {
                    var ids = new List<long>();
                    if (depJe.TryGetProperty("ids", out var idsJe) && idsJe.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var e in idsJe.EnumerateArray())
                        {
                            if (long.TryParse(e.ToString(), out var gid)) ids.Add(gid);
                        }
                    }
                    if (ids.Count > 0)
                    {
                        var depUserIds = await _userDepartmentRepository.GetUserIdsByDepartmentIdsAsync(ids, cancellationToken);
                        candidates.IntersectWith(depUserIds);
                    }
                }

                // 角色
                if (relJe.TryGetProperty("roles", out var roleJe) && roleJe.ValueKind == JsonValueKind.Object)
                {
                    var ids = new List<long>();
                    if (roleJe.TryGetProperty("ids", out var idsJe) && idsJe.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var e in idsJe.EnumerateArray())
                        {
                            if (long.TryParse(e.ToString(), out var gid)) ids.Add(gid);
                        }
                    }
                    if (ids.Count > 0)
                    {
                        var roleUserIds = await _userRoleRepository.GetUserIdsByRoleIdsAsync(ids, cancellationToken);
                        candidates.IntersectWith(roleUserIds);
                    }
                }
            }
            // 字典形式（兼容）
            else if (relObj is IDictionary<string, object?> rel)
            {
                if (rel.TryGetValue("departments", out var depObj) && depObj is IDictionary<string, object?> dep)
                {
                    var ids = dep.TryGetValue("ids", out var v) && v is JsonElement je && je.ValueKind == JsonValueKind.Array
                        ? je.EnumerateArray().Select(x => long.Parse(x.ToString())).ToList()
                        : (dep["ids"] as IEnumerable<object?>)?.Select(x => long.Parse(Convert.ToString(x)!)).ToList() ?? new List<long>();
                    if (ids.Count > 0)
                    {
                        var depUserIds2 = await _userDepartmentRepository.GetUserIdsByDepartmentIdsAsync(ids, cancellationToken);
                        candidates.IntersectWith(depUserIds2);
                    }
                }
                if (rel.TryGetValue("roles", out var roleObj) && roleObj is IDictionary<string, object?> role)
                {
                    var ids = role.TryGetValue("ids", out var v) && v is JsonElement je && je.ValueKind == JsonValueKind.Array
                        ? je.EnumerateArray().Select(x => long.Parse(x.ToString())).ToList()
                        : (role["ids"] as IEnumerable<object?>)?.Select(x => long.Parse(Convert.ToString(x)!)).ToList() ?? new List<long>();
                    if (ids.Count > 0)
                    {
                        var roleUserIds2 = await _userRoleRepository.GetUserIdsByRoleIdsAsync(ids, cancellationToken);
                        candidates.IntersectWith(roleUserIds2);
                    }
                }
            }
        }

        // 将候选结果重新分页（简单方式：在内存中过滤页内集合；可优化为仓储层下推）
        var filtered = baseList.Where(u => candidates.Contains(u.Id)).ToList();
        var total = filtered.Count; // 近似统计
        var pageItems = filtered.Skip((page - 1) * size).Take(size).ToList();

        // 映射到 DTO
        var items = pageItems.Select(x => new UserListItemDto
        {
            Id = x.Id,
            UserName = x.UserName,
            DisplayName = x.DisplayName,
            Email = x.Email,
            Phone = x.Phone,
            CreatedAt = x.CreatedAt,
            Enabled = x.Enabled
        }).ToList();

        // 批量补齐角色/部门名称（顺序执行避免 MySQL 连接复用问题）
        var userIds = pageItems.Select(u => u.Id).ToList();

        // 角色 Ids - 顺序执行
        var roleIdsByUser = new Dictionary<long, List<long>>();
        foreach (var uid in userIds)
        {
            roleIdsByUser[uid] = await _userRoleRepository.GetRoleIdsAsync(uid, cancellationToken);
        }
        var allRoleIds = roleIdsByUser.SelectMany(kv => kv.Value).Distinct().ToList();

        // 部门 Ids - 顺序执行
        var deptIdsByUser = new Dictionary<long, List<long>>();
        foreach (var uid in userIds)
        {
            deptIdsByUser[uid] = await _userDepartmentRepository.GetDepartmentIdsAsync(uid, cancellationToken);
        }
        var allDeptIds = deptIdsByUser.SelectMany(kv => kv.Value).Distinct().ToList();

        var roleMap = allRoleIds.Count == 0
            ? new Dictionary<long, string>()
            : _roleRepoBasic.Query()
                .Where(r => allRoleIds.Contains(r.Id))
                .Select(r => new { r.Id, r.Name })
                .ToList()
                .ToDictionary(x => x.Id, x => x.Name);

        var deptMap = allDeptIds.Count == 0
            ? new Dictionary<long, string>()
            : _deptRepoBasic.Query()
                .Where(d => allDeptIds.Contains(d.Id))
                .Select(d => new { d.Id, d.Name })
                .ToList()
                .ToDictionary(x => x.Id, x => x.Name);

        foreach (var it in items)
        {
            var rids = roleIdsByUser.TryGetValue(it.Id, out var r) ? r : new List<long>();
            it.RoleNames = rids.Select(id => roleMap.TryGetValue(id, out var n) ? n : null)
                               .Where(n => !string.IsNullOrEmpty(n))
                               .Select(n => n!)
                               .ToList();

            var dids = deptIdsByUser.TryGetValue(it.Id, out var d) ? d : new List<long>();
            it.DepartmentNames = dids.Select(id => deptMap.TryGetValue(id, out var n) ? n : null)
                                     .Where(n => !string.IsNullOrEmpty(n))
                                     .Select(n => n!)
                                     .ToList();
        }

        return new PagedResult<UserListItemDto>
        {
            Total = total,
            Page = page,
            PageSize = size,
            Items = items
        };
    }

    /// <inheritdoc />
    public async Task<UserDetailDto?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _userRepoBasic.GetByIdAsync(id, cancellationToken);
        if (entity == null) return null;
        return new UserDetailDto
        {
            Id = entity.Id,
            UserName = entity.UserName,
            DisplayName = entity.DisplayName,
            Avatar = entity.Avatar,
            Introduction = entity.Introduction,
            Email = entity.Email,
            Phone = entity.Phone,
            Enabled = entity.Enabled
        };
    }

    /// <inheritdoc />
    public async Task<long> CreateAsync(CreateUserInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.UserName)) throw new ArgumentException("用户名不能为空");
        if (string.IsNullOrWhiteSpace(input.DisplayName)) throw new ArgumentException("显示名不能为空");
        if (string.IsNullOrWhiteSpace(input.Password) || input.Password.Length < 6) throw new ArgumentException("密码长度至少6位");

        // 检查用户名是否已存在
        var existingUser = await _userRepoBasic.Query()
            .Where(u => u.UserName == input.UserName.Trim())
            .FirstAsync();
        if (existingUser != null)
            throw new ArgumentException($"用户名 '{input.UserName}' 已存在，请使用其他用户名");

        var entity = new User
        {
            UserName = input.UserName.Trim(),
            DisplayName = input.DisplayName.Trim(),
            Email = input.Email?.Trim(),
            Phone = input.Phone?.Trim(),
            Enabled = true
        };
        entity.SetPassword(input.Password, _passwordHasher);
        await _userRepoBasic.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(long id, UpdateUserInput input, CancellationToken cancellationToken = default)
    {
        var entity = await _userRepoBasic.GetByIdAsync(id, cancellationToken);
        if (entity == null) return;

        // 权限边界：仅本人可改；或具备管理员角色方可修改他人
        var me = _currentUser.Id;
        var isAdmin = _currentUser.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
        if (me != id && !isAdmin)
            throw new UnauthorizedAccessException("仅可修改本人资料");

        if (string.IsNullOrWhiteSpace(input.DisplayName)) throw new ArgumentException("显示名不能为空");

        entity.DisplayName = input.DisplayName.Trim();
        entity.Avatar = string.IsNullOrWhiteSpace(input.Avatar) ? null : input.Avatar!.Trim();
        if (input.Introduction != null && input.Introduction.Length > 1000) throw new ArgumentException("个人介绍长度不能超过1000");
        entity.Introduction = string.IsNullOrWhiteSpace(input.Introduction) ? null : input.Introduction!.Trim();
        entity.Email = input.Email?.Trim();
        entity.Phone = input.Phone?.Trim();
        entity.Enabled = input.Enabled;
        await _userRepoBasic.UpdateAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        // 使用硬删除，同时删除关联的角色和部门关系
        await _userRepository.HardDeleteAsync(id, cancellationToken);
    }

    // ===== 关联：角色 =====
    public Task<List<long>> GetUserRoleIdsAsync(long userId, CancellationToken cancellationToken = default)
    {
        return _userRoleRepository.GetRoleIdsAsync(userId, cancellationToken);
    }

    public Task SaveUserRolesAsync(long userId, IEnumerable<long> roleIds, CancellationToken cancellationToken = default)
    {
        return _userRoleRepository.ReplaceAsync(userId, roleIds, cancellationToken);
    }

    // ===== 关联：部门 =====
    public Task<List<long>> GetUserDepartmentIdsAsync(long userId, CancellationToken cancellationToken = default)
    {
        return _userDepartmentRepository.GetDepartmentIdsAsync(userId, cancellationToken);
    }

    public Task SaveUserDepartmentsAsync(long userId, IEnumerable<long> departmentIds, CancellationToken cancellationToken = default)
    {
        return _userDepartmentRepository.ReplaceAsync(userId, departmentIds, cancellationToken);
    }

    /// <summary>
    /// 修改密码：验证旧密码后设置新密码。
    /// </summary>
    public async Task ChangePasswordAsync(long id, ChangePasswordInput input, CancellationToken cancellationToken = default, bool skipOldPasswordCheck = false)
    {
        var user = await _userRepoBasic.GetByIdAsync(id, cancellationToken);
        if (user == null) return;
        var isSelf = _currentUser.Id == id;
        var isAdmin = _currentUser.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
        
        // 本人修改自己的密码必须校验旧密码
        // 不是本人时，如果不是管理员也没有明确跳过，也必须校验（不过通常别人改不了别人的）
        bool bypass = !isSelf && isAdmin;
        if (skipOldPasswordCheck) bypass = true;

        if (!bypass)
        {
            var ok = _passwordHasher.Verify(input.OldPassword, user.PasswordHash, user.Salt);
            if (!ok) throw new ArgumentException("旧密码不正确");
        }
        if (string.IsNullOrWhiteSpace(input.NewPassword) || input.NewPassword.Length < 6) throw new ArgumentException("新密码长度至少6位");

        user.SetPassword(input.NewPassword, _passwordHasher);
        await _userRepoBasic.UpdateAsync(user, cancellationToken);
        return;
    }

    /// <summary>
    /// 前台注册（公开入口）。
    /// </summary>
    public async Task<long> RegisterAsync(RegisterInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.UserName)) throw new ArgumentException("用户名不能为空");
        if (string.IsNullOrWhiteSpace(input.DisplayName)) throw new ArgumentException("显示名不能为空");
        if (!IsStrongPassword(input.Password)) throw new ArgumentException("密码需至少8位，且包含字母和数字");

        var userName = input.UserName.Trim();
        var email = input.Email?.Trim();
        var phone = input.Phone?.Trim();
        if (await _userRepository.ExistsByUserNameAsync(userName, cancellationToken))
            throw new InvalidOperationException("用户名已存在");
        if (!string.IsNullOrEmpty(email) && await _userRepository.ExistsByEmailAsync(email!, cancellationToken))
            throw new InvalidOperationException("邮箱已被使用");

        var user = new User
        {
            UserName = userName,
            DisplayName = input.DisplayName.Trim(),
            Email = email,
            Phone = phone,
            Enabled = true
        };
        user.SetPassword(input.Password, _passwordHasher);
        await _userRepoBasic.AddAsync(user, cancellationToken);

        // 默认角色分配（按 Code=User 或 USER 优先）
        try
        {
            var role = _roleRepoBasic.Query().Where(r => r.Code == "User" || r.Code == "USER").FirstOrDefault();
            if (role != null)
            {
                await _userRoleRepository.ReplaceAsync(user.Id, new[] { role.Id }, cancellationToken);
            }
        }
        catch { /* 角色表不存在或不可用时忽略 */ }

        // 发布“用户注册完成”领域事件（解耦后置初始化逻辑）
        try { await _bus.PublishAsync(new Ginkgo.Domain.Users.Events.UserRegistered(user.Id), cancellationToken); } catch { }
        return user.Id;
    }

    /// <summary>
    /// 检查账户的联系方式，返回脱敏后的邮箱/手机信息。
    /// </summary>
    public async Task<CheckAccountContactOutput> CheckAccountContactAsync(string account, CancellationToken cancellationToken = default)
    {
        var acc = (account ?? string.Empty).Trim();
        User? user = null;
        if (acc.Contains('@')) user = await _userRepository.GetByEmailAsync(acc, cancellationToken);
        user ??= await _userRepository.GetByUserNameAsync(acc, cancellationToken);
        if (user == null) return new CheckAccountContactOutput { Found = false };

        var hasEmail = !string.IsNullOrWhiteSpace(user.Email);
        var hasPhone = !string.IsNullOrWhiteSpace(user.Phone);
        return new CheckAccountContactOutput
        {
            Found = true,
            HasEmail = hasEmail,
            HasPhone = hasPhone,
            MaskedEmail = hasEmail ? MaskEmail(user.Email!) : null,
            MaskedPhone = hasPhone ? MaskPhone(user.Phone!) : null
        };
    }

    /// <summary>
    /// 发起找回密码：通过统一验证码服务向邮箱/手机发送6位数字验证码。
    /// </summary>
    public async Task ForgotPasswordStartAsync(ForgotPasswordStartInput input, CancellationToken cancellationToken = default)
    {
        var account = (input.Account ?? string.Empty).Trim();
        var channelStr = (input.Channel ?? "email").Trim().ToLowerInvariant();
        User? user = null;
        if (account.Contains('@')) user = await _userRepository.GetByEmailAsync(account, cancellationToken);
        user ??= await _userRepository.GetByUserNameAsync(account, cancellationToken);
        // 始终返回成功，避免账户枚举
        if (user == null) return;

        // 根据渠道确定发送目标和渠道类型
        string target;
        var channel = Ginkgo.ServerToolkit.VerificationChannel.Email;
        if (channelStr == "phone" && !string.IsNullOrWhiteSpace(user.Phone))
        {
            target = user.Phone;
            channel = Ginkgo.ServerToolkit.VerificationChannel.Sms;
        }
        else if (!string.IsNullOrWhiteSpace(user.Email))
        {
            target = user.Email;
            channel = Ginkgo.ServerToolkit.VerificationChannel.Email;
        }
        else
        {
            // 用户无有效联系方式，静默返回
            return;
        }

        // 通过统一验证码服务发送（15分钟有效）
        await _secondFactor.SendVerificationCodeAsync(
            target: target,
            purpose: Ginkgo.ServerToolkit.VerificationPurpose.ForgotPassword,
            channel: channel,
            ttlSeconds: 900,
            codeLength: 6,
            throttleSeconds: 60,
            ct: cancellationToken);
    }

    /// <summary>
    /// 完成找回密码：通过统一验证码服务校验验证码并设置新密码。
    /// </summary>
    public async Task ForgotPasswordResetAsync(ForgotPasswordResetInput input, CancellationToken cancellationToken = default)
    {
        if (!IsStrongPassword(input.NewPassword)) throw new ArgumentException("密码需至少8位，且包含字母和数字");

        // 根据 Account 确定验证码校验目标
        var account = (input.Account ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(account))
            throw new ArgumentException("请提供账号信息");

        // 查找用户以获取实际的验证目标（邮箱/手机）
        User? user = null;
        if (account.Contains('@')) user = await _userRepository.GetByEmailAsync(account, cancellationToken);
        user ??= await _userRepository.GetByUserNameAsync(account, cancellationToken);
        if (user == null) throw new InvalidOperationException("用户不存在");

        // 确定验证目标（与发送时一致）
        var target = !string.IsNullOrWhiteSpace(user.Email) ? user.Email : user.Phone;
        if (string.IsNullOrWhiteSpace(target))
            throw new InvalidOperationException("用户无有效联系方式");

        // 通过统一验证码服务校验
        var validateResult = await _secondFactor.ValidateVerificationCodeAsync(
            target: target,
            purpose: Ginkgo.ServerToolkit.VerificationPurpose.ForgotPassword,
            code: (input.Token ?? string.Empty).Trim(),
            ct: cancellationToken);
        if (!validateResult.Success)
            throw new InvalidOperationException(validateResult.Message);

        // 重置密码
        user.SetPassword(input.NewPassword, _passwordHasher);
        await _userRepoBasic.UpdateAsync(user, cancellationToken);
    }

    private static bool IsStrongPassword(string pwd)
    {
        if (string.IsNullOrWhiteSpace(pwd) || pwd.Length < 8) return false;
        bool hasLetter = pwd.Any(char.IsLetter);
        bool hasDigit = pwd.Any(char.IsDigit);
        return hasLetter && hasDigit;
    }

    private static string GenerateSecureToken(int bytes)
    {
        var data = new byte[bytes];
        System.Security.Cryptography.RandomNumberGenerator.Fill(data);
        return Base64UrlEncode(data);
    }

    private static string Sha256(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private static string Base64UrlEncode(byte[] arg)
    {
        string s = Convert.ToBase64String(arg); // Regular base64 encoder
        s = s.Split('=')[0]; // Remove any trailing '='s
        s = s.Replace('+', '-'); // 62nd char of encoding
        s = s.Replace('/', '_'); // 63rd char of encoding
        return s;
    }

    /// <summary>
    /// 生成指定位数的纯数字验证码。
    /// </summary>
    private static string GenerateNumericCode(int length)
    {
        var data = new byte[length];
        System.Security.Cryptography.RandomNumberGenerator.Fill(data);
        var chars = new char[length];
        for (int i = 0; i < length; i++) chars[i] = (char)('0' + data[i] % 10);
        return new string(chars);
    }

    /// <summary>
    /// 邮箱脱敏：e***@g***.com
    /// </summary>
    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0) return "***";
        var local = email[..at];
        var domain = email[(at + 1)..];
        var maskedLocal = local.Length <= 2 ? local[0] + "***" : local[0] + "***" + local[^1];
        var dotIdx = domain.LastIndexOf('.');
        string maskedDomain;
        if (dotIdx > 1)
        {
            maskedDomain = domain[0] + "***" + domain[dotIdx..];
        }
        else
        {
            maskedDomain = domain;
        }
        return maskedLocal + "@" + maskedDomain;
    }

    /// <summary>
    /// 手机号脱敏：138****5678
    /// </summary>
    private static string MaskPhone(string phone)
    {
        if (phone.Length < 7) return "****";
        return phone[..3] + "****" + phone[^4..];
    }

}


