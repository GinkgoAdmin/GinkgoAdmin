// 文件功能说明：
// 提供认证接口：登录获取 JWT 令牌 + Refresh Token。

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Ginkgo.Api.Auth;
using Ginkgo.Application.Users;
using Ginkgo.Domain.Events;
using Ginkgo.Domain.Users.Events;
using Ginkgo.Domain;
using Ginkgo.Domain.Auth;
using Ginkgo.Domain.Users;
using Ginkgo.Plugin.Abstractions.Extensions;
using Ginkgo.Shared;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Ginkgo.Api.Controllers;

/// <summary>
/// 认证接口。
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IRepository<User> _userRepo;
    private readonly IRepository<RefreshToken> _refreshTokenRepo;
    private readonly JwtOptions _jwtOptions;

    private readonly IUserAppService _userApp;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDomainEventPublisher _bus;
    private readonly IServiceProvider _serviceProvider;
    private readonly PermissionCacheInvalidator _permissionCacheInvalidator;

    public AuthController(
        IRepository<User> userRepo,
        IRepository<RefreshToken> refreshTokenRepo,
        IOptions<JwtOptions> jwtOptions,
        IUserAppService userApp,
        IPasswordHasher passwordHasher,
        IDomainEventPublisher bus,
        IServiceProvider serviceProvider,
        PermissionCacheInvalidator permissionCacheInvalidator)
    {
        _userRepo = userRepo;
        _refreshTokenRepo = refreshTokenRepo;
        _jwtOptions = jwtOptions.Value;
        _userApp = userApp;
        _passwordHasher = passwordHasher;
        _bus = bus;
        _serviceProvider = serviceProvider;
        _permissionCacheInvalidator = permissionCacheInvalidator;
    }

    /// <summary>
    /// 登录并发放令牌（加盐哈希校验）。
    /// </summary>
    /// <param name="userName">用户名。</param>
    /// <param name="password">密码。</param>
    /// <param name="clientType">客户端类型（默认 WEB_ADMIN）。</param>
    /// <param name="db">数据库客户端（由 DI 注入）。</param>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<Result<object>> LoginAsync([FromForm] string userName, [FromForm] string password, [FromForm] string? clientType, [FromServices] SqlSugar.ISqlSugarClient db)
    {
        // 客户端类型：默认 WEB_ADMIN
        var client = string.IsNullOrWhiteSpace(clientType) ? "WEB_ADMIN" : clientType.Trim().ToUpperInvariant();
        // 支持用户名、邮箱、手机号三种方式登录
        var sql = @"SELECT Id, UserName, DisplayName, PasswordHash, Salt, Email, Phone, Enabled, LastLoginAt, Avatar, Introduction 
                     FROM ginkgo_Sys_User 
                     WHERE (UserName = @Account OR Email = @Account OR Phone = @Account) 
                       AND Enabled = 1 AND IsDeleted = 0";
        var dt = await db.Ado.GetDataTableAsync(sql, new { Account = userName });
        
        if (dt.Rows.Count == 0)
        {
            HttpContext.Items["OpLogResult"] = "用户名或密码错误";
            return Result<object>.Fail(4001, "用户名或密码错误");
        }
        
        var row = dt.Rows[0];
        
        // 根据实际类型处理 Id（支持 bigint/decimal/int）
        var idValue = row["Id"];
        long userId;
        if (idValue is long longId)
            userId = longId;
        else if (idValue is decimal decimalId)
            userId = (long)decimalId;
        else if (idValue is int intId)
            userId = intId;
        else
            return Result<object>.Fail(5001, $"数据库 Id 类型不匹配: {idValue?.GetType().Name}");
        
        var user = new User
        {
            Id = userId,
            UserName = row["UserName"]?.ToString() ?? string.Empty,
            DisplayName = row["DisplayName"]?.ToString() ?? string.Empty,
            PasswordHash = row["PasswordHash"]?.ToString() ?? string.Empty,
            Salt = row["Salt"] == DBNull.Value ? null : row["Salt"]?.ToString(),
            Email = row["Email"] == DBNull.Value ? null : row["Email"]?.ToString(),
            Phone = row["Phone"] == DBNull.Value ? null : row["Phone"]?.ToString(),
            Enabled = Convert.ToBoolean(row["Enabled"]),
            LastLoginAt = row["LastLoginAt"] == DBNull.Value ? null : Convert.ToDateTime(row["LastLoginAt"]),
            Avatar = row["Avatar"] == DBNull.Value ? null : row["Avatar"]?.ToString(),
            Introduction = row["Introduction"] == DBNull.Value ? null : row["Introduction"]?.ToString()
        };
        var verified = _passwordHasher.Verify(password, user.PasswordHash, user.Salt);
        if (!verified)
        {
            // Backward compatibility: legacy SHA256(password + ":" + saltBase64)
            if (!string.IsNullOrEmpty(user.Salt))
            {
                var legacy = ComputeLegacySha256Base64(password, user.Salt!);
                if (string.Equals(legacy, user.PasswordHash, StringComparison.Ordinal))
                {
                    // Upgrade to PBKDF2 on-the-fly
                    var newHash = _passwordHasher.Hash(password, out var newSalt);
                    user.PasswordHash = newHash;
                    user.Salt = newSalt;
                    await _userRepo.UpdateAsync(user);
                    verified = true;
                }
            }
        }
        if (!verified)
        {
            HttpContext.Items["OpLogResult"] = "密码错误";
            return Result<object>.Fail(4001, "用户名或密码错误");
        }
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("name", user.DisplayName),
            new Claim("uname", user.UserName)
        };

        // 查询用户所属角色是否有超级管理员标记（IsSuperAdmin=1）
        var superAdminCheckSql = @"
            SELECT COUNT(1) FROM ginkgo_Sys_Role r
            INNER JOIN ginkgo_Sys_UserRole ur ON ur.RoleId = r.Id
            WHERE ur.UserId = @UserId AND r.IsDeleted = 0 AND r.Enabled = 1 AND r.IsSuperAdmin = 1";
        var isSuperAdmin = await db.Ado.GetIntAsync(superAdminCheckSql, new { UserId = userId }) > 0;

        if (isSuperAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "ADMIN"));
        }

        // ===== 模块扩展点：IJwtClaimsContributor =====
        // 收集所有模块注册的 JWT Claims 贡献者，向令牌添加自定义 Claims。
        await AppendModuleClaimsAsync(claims, userId);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.Now.AddMinutes(_jwtOptions.ExpiresMinutes);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        var jwt = _tokenHandler.WriteToken(token);
        user.LastLoginAt = DateTime.Now;
        // 更新用户最后登录时间
        await _userRepo.UpdateAsync(user);

        // 通过发布领域事件记录“登录成功”
        try
        {
            var eventIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _bus.PublishAsync(new UserLoggedIn(user.Id, user.UserName, eventIp), HttpContext.RequestAborted);
        }
        catch { /* 忽略事件异常，不影响登录返回 */ }

        // ===== 客户端登录权限验证 =====
        // 超级管理员跳过客户端验证
        if (!isSuperAdmin)
        {
            var clientCheckSql = @"
                SELECT r.AllowedClients 
                FROM ginkgo_Sys_Role r 
                INNER JOIN ginkgo_Sys_UserRole ur ON ur.RoleId = r.Id 
                WHERE ur.UserId = @UserId AND r.IsDeleted = 0 AND r.Enabled = 1";
            var roleDt = await db.Ado.GetDataTableAsync(clientCheckSql, new { UserId = userId });
            
            // 无角色 → 禁止登录
            if (roleDt.Rows.Count == 0)
            {
                HttpContext.Items["OpLogResult"] = "未分配角色";
                return Result<object>.Fail(4003, "当前账户未分配角色，无法登录");
            }

            var hasPermission = false;
            foreach (System.Data.DataRow roleRow in roleDt.Rows)
            {
                var allowed = roleRow["AllowedClients"] == DBNull.Value ? null : roleRow["AllowedClients"]?.ToString();
                // NULL 或空 = 该角色不允许任何客户端登录，跳过
                if (string.IsNullOrWhiteSpace(allowed))
                    continue;
                // 检查逗号分隔列表中是否包含当前客户端
                var allowedList = allowed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (allowedList.Any(a => string.Equals(a, client, StringComparison.OrdinalIgnoreCase)))
                {
                    hasPermission = true;
                    break;
                }
            }
            if (!hasPermission)
            {
                HttpContext.Items["OpLogResult"] = "无权登录此客户端";
                return Result<object>.Fail(4003, "当前账户无权登录此客户端");
            }
        }

        // 读取用户角色
        var roles = new List<string>();
        if (isSuperAdmin)
        {
            roles.Add("ADMIN");
        }

        // 生成 Refresh Token（一次性轮换）
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var refreshToken = await GenerateRefreshTokenAsync(userId, ip);
        _permissionCacheInvalidator.InvalidateAll();

        return Result<object>.Success(new {
            token = jwt,
            refreshToken = refreshToken.Token,
            expiresAt = expires,
            userName = user.UserName,
            displayName = user.DisplayName,
            avatar = user.Avatar,
            phone = user.Phone,
            email = user.Email,
            roles,
            isSuperAdmin
        }, "登录成功");
    }

    /// <summary>
    /// 退出登录：吊销所有 Refresh Token 并记录日志。
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<Result> LogoutAsync()
    {
        long? userId = null;
        var uid = HttpContext.User?.Claims.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier") || c.Type.EndsWith("/sub"))?.Value;
        if (long.TryParse(uid, out var gid)) userId = gid;
        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (userId != null)
            {
                // 吊销该用户所有有效的 Refresh Token
                var activeTokens = _refreshTokenRepo.Query()
                    .Where(t => t.UserId == userId.Value && !t.IsRevoked && t.ExpiresAt > DateTime.Now)
                    .ToList();
                foreach (var t in activeTokens)
                {
                    t.IsRevoked = true;
                    t.RevokedAt = DateTime.Now;
                    await _refreshTokenRepo.UpdateAsync(t);
                }
                await _bus.PublishAsync(new UserLoggedOut(userId.Value, HttpContext.User?.Identity?.Name, ip), HttpContext.RequestAborted);
            }
        }
        catch { }
        return Result.Success("已退出");
    }

    /// <summary>
    /// 刷新令牌：使用 Refresh Token 获取新的 Access Token + Refresh Token（一次性轮换）。
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<Result<object>> RefreshAsync([FromBody] RefreshTokenInput input)
    {
        if (string.IsNullOrWhiteSpace(input?.RefreshToken))
            return Result<object>.Fail(4001, "Refresh Token 不能为空");

        var existing = _refreshTokenRepo.Query()
            .Where(t => t.Token == input.RefreshToken)
            .First();

        if (existing == null)
            return Result<object>.Fail(4001, "无效的 Refresh Token");

        if (existing.IsRevoked)
        {
            // 被吊销的令牌被再次使用 → 可能令牌泄露，吊销该用户全部令牌
            var family = _refreshTokenRepo.Query()
                .Where(t => t.UserId == existing.UserId && !t.IsRevoked)
                .ToList();
            foreach (var t in family)
            {
                t.IsRevoked = true;
                t.RevokedAt = DateTime.Now;
                await _refreshTokenRepo.UpdateAsync(t);
            }
            return Result<object>.Fail(4001, "Refresh Token 已被吊销，所有会话已失效");
        }

        if (DateTime.Now >= existing.ExpiresAt)
            return Result<object>.Fail(4001, "Refresh Token 已过期，请重新登录");

        // 查找用户
        var user = _userRepo.Query().Where(u => u.Id == existing.UserId).First();
        if (user == null || !user.Enabled || user.IsDeleted)
            return Result<object>.Fail(4001, "用户不存在或已禁用");

        // 吊销旧令牌
        existing.IsRevoked = true;
        existing.RevokedAt = DateTime.Now;

        // 生成新令牌对
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var newRefreshToken = await GenerateRefreshTokenAsync(existing.UserId, ip);
        existing.ReplacedByToken = newRefreshToken.Token;
        await _refreshTokenRepo.UpdateAsync(existing);

        // 签发新 Access Token
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("name", user.DisplayName ?? ""),
            new Claim("uname", user.UserName ?? "")
        };
        // 查询用户所属角色是否有超级管理员标记
        var refreshDb = HttpContext.RequestServices.GetRequiredService<SqlSugar.ISqlSugarClient>();
        var refreshSuperAdminSql = @"
            SELECT COUNT(1) FROM ginkgo_Sys_Role r
            INNER JOIN ginkgo_Sys_UserRole ur ON ur.RoleId = r.Id
            WHERE ur.UserId = @UserId AND r.IsDeleted = 0 AND r.Enabled = 1 AND r.IsSuperAdmin = 1";
        var isRefreshSuperAdmin = await refreshDb.Ado.GetIntAsync(refreshSuperAdminSql, new { UserId = existing.UserId }) > 0;
        if (isRefreshSuperAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "ADMIN"));

        // 模块扩展点：IJwtClaimsContributor
        await AppendModuleClaimsAsync(claims, existing.UserId);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.Now.AddMinutes(_jwtOptions.ExpiresMinutes);
        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return Result<object>.Success(new
        {
            token = _tokenHandler.WriteToken(token),
            refreshToken = newRefreshToken.Token,
            expiresAt = expires,
            isSuperAdmin = isRefreshSuperAdmin
        }, "刷新成功");
    }

    /// <summary>
    /// 主动吊销 Refresh Token（用于安全登出）。
    /// </summary>
    [HttpPost("revoke")]
    public async Task<Result> RevokeAsync([FromBody] RefreshTokenInput input)
    {
        if (string.IsNullOrWhiteSpace(input?.RefreshToken))
            return Result.Fail(4001, "Refresh Token 不能为空");

        var existing = _refreshTokenRepo.Query()
            .Where(t => t.Token == input.RefreshToken)
            .First();

        if (existing == null || existing.IsRevoked)
            return Result.Success("已处理");

        existing.IsRevoked = true;
        existing.RevokedAt = DateTime.Now;
        await _refreshTokenRepo.UpdateAsync(existing);
        return Result.Success("已吊销");
    }

    /// <summary>
    /// 用户注册（公开）。根据后台 Registration.Mode 配置验证必填字段和验证码。
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<Result<long>> RegisterAsync(
        [FromBody] RegisterInput input,
        [FromServices] SqlSugar.ISqlSugarClient db,
        [FromServices] Ginkgo.ServerToolkit.ISecondaryVerificationService verificationService,
        CancellationToken ct)
    {
        // 读取注册模式配置
        var modeSetting = await db.Ado.GetStringAsync(
            "SELECT `Value` FROM ginkgo_Sys_Settings WHERE `Key` = 'Registration.Mode' LIMIT 1");
        var mode = string.IsNullOrWhiteSpace(modeSetting) ? "free" : modeSetting.Trim().ToLowerInvariant();

        // 关闭注册
        if (mode == "disabled")
            return Result<long>.Fail(4003, "当前系统已关闭自助注册");

        // 根据模式验证必填字段
        var needEmail = mode is "email_code" or "both_code";
        var needPhone = mode is "phone_code" or "both_code";
        var needEmailCode = mode is "email_code" or "both_code";
        var needPhoneCode = mode is "phone_code" or "both_code";

        if (needEmail && string.IsNullOrWhiteSpace(input.Email))
            return Result<long>.Fail(4001, "当前注册模式要求填写邮箱");
        if (needPhone && string.IsNullOrWhiteSpace(input.Phone))
            return Result<long>.Fail(4001, "当前注册模式要求填写手机号");

        // 邮箱/手机注册模式下，用户名为空时自动用邮箱或手机号填充
        if (string.IsNullOrWhiteSpace(input.UserName) && mode != "free")
        {
            input.UserName = (input.Email?.Trim() ?? input.Phone?.Trim()) ?? "";
        }
        if (string.IsNullOrWhiteSpace(input.DisplayName))
        {
            input.DisplayName = input.UserName;
        }

        // 验证邮箱验证码（使用统一验证码服务，与发送时的 purpose 一致）
        if (needEmailCode)
        {
            if (string.IsNullOrWhiteSpace(input.EmailCode))
                return Result<long>.Fail(4001, "请输入邮箱验证码");
            var result = await verificationService.ValidateVerificationCodeAsync(
                target: input.Email!.Trim(),
                purpose: Ginkgo.ServerToolkit.VerificationPurpose.Register,
                code: input.EmailCode.Trim(),
                consumeOnSuccess: true,
                ct: ct);
            if (!result.Success)
                return Result<long>.Fail(4001, result.Message ?? "邮箱验证码无效或已过期");
        }

        // 验证手机验证码
        if (needPhoneCode)
        {
            if (string.IsNullOrWhiteSpace(input.PhoneCode))
                return Result<long>.Fail(4001, "请输入手机验证码");
            var result = await verificationService.ValidateVerificationCodeAsync(
                target: input.Phone!.Trim(),
                purpose: Ginkgo.ServerToolkit.VerificationPurpose.Register,
                code: input.PhoneCode.Trim(),
                consumeOnSuccess: true,
                ct: ct);
            if (!result.Success)
                return Result<long>.Fail(4001, result.Message ?? "手机验证码无效或已过期");
        }

        var id = await _userApp.RegisterAsync(input, ct);
        return Result<long>.Success(id, "注册成功");
    }

    /// <summary>
    /// 检查账户的联系方式（找回密码前置检查）。
    /// </summary>
    [HttpPost("password/check-contact")]
    [AllowAnonymous]
    public async Task<Result<CheckAccountContactOutput>> CheckAccountContactAsync([FromBody] ForgotPasswordStartInput input, CancellationToken ct)
    {
        var result = await _userApp.CheckAccountContactAsync(input.Account, ct);
        return Result<CheckAccountContactOutput>.Success(result);
    }

    /// <summary>
    /// 发起找回密码（发送验证码）。
    /// </summary>
    [HttpPost("password/forgot")]
    [AllowAnonymous]
    public async Task<Result> ForgotPasswordStartAsync([FromBody] ForgotPasswordStartInput input, CancellationToken ct)
    {
        await _userApp.ForgotPasswordStartAsync(input, ct);
        return Result.Success("验证码已发送");
    }

    /// <summary>
    /// 完成找回密码（提交验证码与新密码）。
    /// </summary>
    [HttpPost("password/reset")]
    [AllowAnonymous]
    public async Task<Result> ForgotPasswordResetAsync([FromBody] ForgotPasswordResetInput input, CancellationToken ct)
    {
        await _userApp.ForgotPasswordResetAsync(input, ct);
        return Result.Success("已重置密码");
    }

    /// <summary>
    /// 生成并持久化 Refresh Token。
    /// </summary>
    private async Task<RefreshToken> GenerateRefreshTokenAsync(long userId, string? ip)
    {
        var tokenBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(tokenBytes);

        var refreshToken = new RefreshToken
        {
            Token = Convert.ToBase64String(tokenBytes),
            UserId = userId,
            ExpiresAt = DateTime.Now.AddMinutes(_jwtOptions.RefreshTokenExpiresMinutes),
            CreatedByIp = ip
        };
        await _refreshTokenRepo.AddAsync(refreshToken);
        return refreshToken;
    }

    private static string ComputeLegacySha256Base64(string password, string saltBase64)
    {
        var bytes = Encoding.UTF8.GetBytes(password + ":" + saltBase64);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// 模块扩展点：收集所有 IJwtClaimsContributor 的 Claims 并追加到列表中。
    /// </summary>
    private async Task AppendModuleClaimsAsync(List<Claim> claims, long userId)
    {
        var contributors = _serviceProvider.GetServices<IJwtClaimsContributor>()
            ?.OrderBy(c => c.Order)
            .ToList();
        if (contributors == null || contributors.Count == 0) return;

        foreach (var contributor in contributors)
        {
            try
            {
                var extraClaims = await contributor.GetAdditionalClaimsAsync(userId, _serviceProvider);
                if (extraClaims != null)
                    claims.AddRange(extraClaims);
            }
            catch { /* 模块 Claims 贡献者异常不影响登录流程 */ }
        }
    }

    private static readonly JwtSecurityTokenHandler _tokenHandler = new();
}

/// <summary>
/// Refresh Token 请求体。
/// </summary>
public sealed class RefreshTokenInput
{
    public string RefreshToken { get; set; } = string.Empty;
}
