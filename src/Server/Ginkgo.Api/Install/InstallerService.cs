// 文件功能说明：
// 一键安装服务。原本散落在此处的"按数据库类型分流"逻辑（MySql / SqlServer 私有方法、
// 反引号 INSERT、SqlConnectionStringBuilder 直接调用、按 GO/; 切批 等）已全部下沉至
// Ginkgo.Infrastructure.Dialects 内的 IDatabaseDialect 实现。
//
// 本文件现在只承担"安装编排"职责：
//   1) 通过 IDialectRegistry 取到当前数据库方言
//   2) 用 dialect 完成建库 / 切批 / INSERT 清洗 / 连接串规整化 / 删库回滚
//   3) 执行业务侧的种子配置（管理员账号、ADMIN 角色绑定、Site.Name 等）

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Ginkgo.Domain;
using Ginkgo.Domain.Settings;
using Ginkgo.Domain.Users;
using Ginkgo.Infrastructure.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Ginkgo.Api.Install;

/// <summary>
/// 银杏科技（https://ginkgo.hhyx.xyz）一键安装服务。
/// </summary>
public sealed class InstallerService
{
    private readonly ILogger<InstallerService> _logger;
    private readonly IHostEnvironment _env;
    private readonly IDialectRegistry _dialectRegistry;

    public InstallerService(
        ILogger<InstallerService> logger,
        IHostEnvironment env,
        IDialectRegistry dialectRegistry)
    {
        _logger = logger;
        _env = env;
        _dialectRegistry = dialectRegistry;
    }

    private string? _resourceDirectory;
    public string ResourceDirectory
    {
        get
        {
            if (!string.IsNullOrEmpty(_resourceDirectory)) return _resourceDirectory!;
            // 优先使用项目 ContentRoot 下的 resource 目录
            var local = Path.Combine(_env.ContentRootPath, "resource");
            if (Directory.Exists(local)) { _resourceDirectory = local; return _resourceDirectory; }
            // 与 Program.cs 静态资源映射保持一致的回退：尝试仓库根目录 resource
            var fallback = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "..", "..", "resource"));
            if (Directory.Exists(fallback)) { _resourceDirectory = fallback; return _resourceDirectory; }
            // 都不存在时仍返回本地路径，后续可创建
            _resourceDirectory = local;
            return _resourceDirectory;
        }
    }
    public string LockFilePath => Path.Combine(ResourceDirectory, "install.lock");

    public bool IsInstalled() => File.Exists(LockFilePath);

    private static void Log(List<InstallLog> logs, string msg, string level = "INFO")
        => logs.Add(new InstallLog { At = DateTime.Now, Level = level, Message = msg });

    /// <summary>
    /// 创建安装期用的 SqlSugarClient。安装时 DI 容器中尚未注册 ISqlSugarClient
    /// （因为还没建好数据库），所以这里临时按 dialect 直接构建。
    /// </summary>
    private static SqlSugarClient CreateInstallClient(IDatabaseDialect dialect, string connString)
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            DbType = dialect.SqlSugarDbType,
            ConnectionString = connString,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = true
        });
    }

    public async Task<InstallResult> InstallAsync(InstallRequest input, CancellationToken ct = default)
    {
        var result = new InstallResult();
        Directory.CreateDirectory(ResourceDirectory);
        if (IsInstalled()) { result.AlreadyInstalled = true; Log(result.Logs, "系统已安装，跳过。", "WARN"); return result; }

        // 基本校验
        if (string.IsNullOrWhiteSpace(input.ConnectionString)) { result.Error = "连接字符串不能为空"; return result; }
        if (string.IsNullOrWhiteSpace(input.AdminUserName) || string.IsNullOrWhiteSpace(input.AdminPassword))
        { result.Error = "管理员用户名/密码不能为空"; return result; }

        // 解析数据库方言（未注册的 provider 会抛出明确异常）
        IDatabaseDialect dialect;
        try
        {
            dialect = _dialectRegistry.Get(input.Provider);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            Log(result.Logs, ex.Message, "ERROR");
            return result;
        }

        Log(result.Logs, $"开始一键安装（数据库方言：{dialect.DisplayName}）...");
        SqlSugarClient db = CreateInstallClient(dialect, input.ConnectionString);

        try
        {
            // 1) 建库
            var dbName = dialect.TryGetDatabaseName(input.ConnectionString) ?? string.Empty;
            Log(result.Logs, string.IsNullOrEmpty(dbName) ? "检查/创建数据库..." : $"检查/创建数据库：{dbName}...");

            bool createdNewDb = false;
            try
            {
                createdNewDb = await dialect.CreateDatabaseIfNotExistsAsync(input.ConnectionString, dbName, ct);
                Log(result.Logs, createdNewDb ? $"已创建数据库：{dbName}" : $"数据库已存在：{dbName}，跳过创建。");
            }
            catch (Exception ex)
            {
                Log(result.Logs, $"创建数据库失败：{ex.Message}", "ERROR");
                throw;
            }

            // 2) 初始化表结构与基础数据：按方言选择对应的静态脚本（不做运行时转换）
            var scriptFile = dialect.InstallScriptResourceName;
            var scriptPath = FindResourceFile(scriptFile);

            // 启动事务：脚本执行 + 基础配置（失败则回滚）
            db.Ado.BeginTran();
            try
            {
                if (!string.IsNullOrEmpty(scriptPath) && File.Exists(scriptPath))
                {
                    Log(result.Logs, $"执行安装脚本: {scriptPath}");
                    await ExecuteSqlScriptAsync(db, dialect, scriptPath, ct, result.Logs);
                }
                else
                {
                    Log(result.Logs, $"未找到脚本 {scriptFile}，回退到 CodeFirst 建表...");
                    var entityBase = typeof(Entity);
                    var entityTypes = entityBase.Assembly.GetTypes()
                        .Where(t => t.IsClass && !t.IsAbstract && entityBase.IsAssignableFrom(t))
                        .ToArray();
                    db.CodeFirst.InitTables(entityTypes);
                }

                // 3) 初始化菜单数据
                var menuScriptFile = dialect.InitMenusScriptResourceName;
                var menuScriptPath = FindResourceFile(menuScriptFile);
                if (!string.IsNullOrEmpty(menuScriptPath) && File.Exists(menuScriptPath))
                {
                    Log(result.Logs, $"执行菜单初始化脚本: {menuScriptPath}");
                    await ExecuteSqlScriptAsync(db, dialect, menuScriptPath, ct, result.Logs);
                }
                else
                {
                    Log(result.Logs, $"未找到菜单初始化脚本 {menuScriptFile}，跳过菜单初始化", "WARN");
                }

                // 基础配置
                Log(result.Logs, "写入基础站点配置 (仅 Site.Name)...");
                await UpsertSettingAsync(db, "Site.Name", input.SiteName ?? "Ginkgo", type: "String", @class: "Site");

                Log(result.Logs, "创建/更新管理员账号...");
                await CreateOrUpdateAdminAsync(db,
                    input.AdminUserName.Trim(),
                    input.AdminPassword,
                    input.AdminDisplayName?.Trim() ?? input.AdminUserName.Trim(),
                    input.AdminEmail?.Trim());

                // 自动给管理员分配 ADMIN 角色，确保安装后能直接登录
                Log(result.Logs, "分配管理员角色...");
                var adminUser = await db.Queryable<User>()
                    .Where(u => u.UserName == input.AdminUserName.Trim() && (u as AuditableEntity).IsDeleted == false)
                    .FirstAsync();
                if (adminUser != null)
                {
                    const long adminRoleId = 200000000000001; // ADMIN 角色的预设 ID
                    var hasRole = await db.Queryable<Ginkgo.Domain.Users.UserRole>()
                        .Where(ur => ur.UserId == adminUser.Id && ur.RoleId == adminRoleId)
                        .AnyAsync();
                    if (!hasRole)
                    {
                        var newId = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId();
                        var userRoleTable = dialect.QuoteTable(null, "ginkgo_Sys_UserRole");
                        var sqlInsertUserRole =
                            $"INSERT INTO {userRoleTable} " +
                            $"({dialect.QuoteIdentifier("Id")},{dialect.QuoteIdentifier("UserId")},{dialect.QuoteIdentifier("RoleId")},{dialect.QuoteIdentifier("CreatedAt")}) " +
                            "VALUES (@Id,@UserId,@RoleId,@CreatedAt)";
                        await db.Ado.ExecuteCommandAsync(
                            sqlInsertUserRole,
                            new[] {
                                new SugarParameter("@Id", newId),
                                new SugarParameter("@UserId", adminUser.Id),
                                new SugarParameter("@RoleId", adminRoleId),
                                new SugarParameter("@CreatedAt", DateTime.Now)
                            });
                        Log(result.Logs, $"已将用户 {input.AdminUserName} 分配到 ADMIN 角色");
                    }

                    // 确保 ADMIN 角色为超级管理员且拥有所有客户端登录权限
                    var roleTable = dialect.QuoteTable(null, "ginkgo_Sys_Role");
                    var isSuperAdmin = dialect.QuoteIdentifier("IsSuperAdmin");
                    var allowedClients = dialect.QuoteIdentifier("AllowedClients");
                    var idCol = dialect.QuoteIdentifier("Id");
                    var sqlUpdateRole =
                        $"UPDATE {roleTable} SET {isSuperAdmin} = 1, " +
                        $"{allowedClients} = COALESCE(NULLIF({allowedClients},''), @Clients) " +
                        $"WHERE {idCol} = @RoleId";
                    await db.Ado.ExecuteCommandAsync(
                        sqlUpdateRole,
                        new[] {
                            new SugarParameter("@Clients", "WEB_ADMIN,WEB_PORTAL,WPF,UNIAPP"),
                            new SugarParameter("@RoleId", adminRoleId)
                        });
                }

                db.Ado.CommitTran();
            }
            catch (Exception exAll)
            {
                try { db.Ado.RollbackTran(); Log(result.Logs, "安装失败，已回滚全部数据库变更。", "ERROR"); } catch { }

                // 若本次安装新建了数据库，则一并删除（由方言决定具体实现）
                if (createdNewDb && !string.IsNullOrEmpty(dbName))
                {
                    Log(result.Logs, $"因失败回滚，删除新建数据库：{dbName}...", "ERROR");
                    try { await dialect.DropDatabaseAsync(input.ConnectionString, dbName, ct); } catch { /* swallow */ }
                }
                throw new InvalidOperationException($"安装失败（已回滚）：{exAll.Message}", exAll);
            }

            // 5.5) 生成随机管理后台路径并更新前端配置
            string adminSlug = GenerateRandomSlug(10);
            try
            {
                await UpdateAdminSlugAsync(adminSlug, result.Logs);
            }
            catch (Exception slugEx)
            {
                Log(result.Logs, $"更新管理后台路径失败（不影响安装）：{slugEx.Message}", "WARN");
            }
            result.AdminSlug = adminSlug;

            // 6) 生成锁定文件
            Log(result.Logs, $"ResourceDirectory: {ResourceDirectory}");
            Log(result.Logs, "写入锁定文件...");
            var content = $"installed_at={DateTime.Now:O}\nprovider={input.Provider}\nadmin={input.AdminUserName}\nadmin_slug={adminSlug}\n";
            await File.WriteAllTextAsync(LockFilePath, content, ct);
            Log(result.Logs, $"锁定文件已写入: {LockFilePath}");

            // 写入安装后的数据库配置，供应用重启后自动连接
            try
            {
                var dbCfgPath = Path.Combine(ResourceDirectory, "db.json");
                Log(result.Logs, $"准备写入数据库配置到: {dbCfgPath}");

                // 连接串规整化交给方言（MySQL 补 utf8mb4、SQL Server 补 MultipleActiveResultSets 等）
                var csOut = dialect.NormalizeConnectionString(input.ConnectionString);

                // 生成随机 JWT 密钥（64 字符）
                var jwtKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
                Log(result.Logs, $"生成的 JWT 密钥长度: {jwtKey.Length}");

                // 使用 DbJsonWriter 以 JSONC 风格写出：每个配置项独占一行，上面一行是中文 // 注释；
                // Database.Features 全量展开（BulkOps / SlowQuery 默认启用，其余能力默认关闭）。
                var dbCfgJson = Ginkgo.Api.Install.Writers.DbJsonWriter.Build(
                    jwtSigningKey: jwtKey,
                    jwtIssuer: "ginkgo",
                    jwtAudience: "ginkgo-clients",
                    jwtExpiresMinutes: 120,
                    dbProvider: input.Provider,
                    dbConnectionString: csOut);
                await File.WriteAllTextAsync(dbCfgPath, dbCfgJson, ct);
                Log(result.Logs, $"已写入数据库配置: {dbCfgPath}");

                // 验证文件是否写入成功
                if (File.Exists(dbCfgPath))
                {
                    var written = await File.ReadAllTextAsync(dbCfgPath, ct);
                    Log(result.Logs, $"验证: db.json 文件大小 {written.Length} 字节");
                }
            }
            catch (Exception ex)
            {
                Log(result.Logs, $"写入数据库配置失败（不影响安装完成）：{ex.Message}", "WARN");
                Log(result.Logs, $"异常详情: {ex}", "ERROR");
            }

            // 提示：安装完成后需重启后端以应用新配置（resource/db.json）
            Log(result.Logs, "安装已完成。请重启 API 服务以载入新的数据库配置（resource/db.json）。若 /swagger 返回 404，说明服务尚未重启。", "WARN");
            Log(result.Logs, "调试环境启动：dotnet run --project src/Server/Ginkgo.Api/Ginkgo.Api.csproj -c Debug");
            Log(result.Logs, "调试环境停止：在运行窗口按 Ctrl+C，或执行：taskkill /F /IM Ginkgo.Api.exe");
            Log(result.Logs, "生产环境发布：dotnet publish src/Server/Ginkgo.Api/Ginkgo.Api.csproj -c Release -o publish");
            Log(result.Logs, "生产环境启动：(PowerShell) $env:ASPNETCORE_URLS=\"http://0.0.0.0:5288\"; dotnet .\\publish\\Ginkgo.Api.dll");
            Log(result.Logs, "生产环境停止：Ctrl+C 或 taskkill /F /IM Ginkgo.Api.exe");

            result.Success = true;
            Log(result.Logs, "安装完成。", "SUCCESS");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "安装失败: {Message}", ex.Message);
            result.Error = ex.Message;
            Log(result.Logs, ex.ToString(), "ERROR");
            return result;
        }
        finally
        {
            try { db?.Dispose(); } catch { }
        }
    }

    private static async Task UpsertSettingAsync(ISqlSugarClient db, string key, string value, string? type = null, string? description = null, string? @class = null)
    {
        // 注意：当前数据库主键为 Key，故以 Key 唯一进行查重，避免主键冲突
        var exists = await db.Queryable<Setting>().Where(s => s.Key == key).FirstAsync();

        if (exists == null)
        {
            // 使用领域工厂，避免直接设置私有 setter
            var entity = Setting.Create(key, value, type, description, @class, operatorId: null, nowUtc: DateTime.Now);
            await db.Insertable(entity).IgnoreColumns(i => i.RowVersion).ExecuteCommandAsync();
        }
        else
        {
            // 使用领域行为更新数值与元信息
            exists.SetValue(value, type, operatorId: null, nowUtc: DateTime.Now);
            // 如传入了描述或分类，则更新元信息；未传则保留现有
            if (!string.IsNullOrWhiteSpace(description) || !string.IsNullOrWhiteSpace(@class))
            {
                exists.ChangeMeta(description ?? exists.Description, @class ?? exists.Class, operatorId: null, nowUtc: DateTime.Now);
            }
            await db.Updateable(exists).IgnoreColumns(i => i.RowVersion).ExecuteCommandAsync();
        }
    }

    private static async Task CreateOrUpdateAdminAsync(ISqlSugarClient db, string userName, string password, string displayName, string? email)
    {
        var exists = await db.Queryable<User>().Where(u => u.UserName == userName && (u as AuditableEntity).IsDeleted == false).FirstAsync();
        var saltBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var hash = ComputeSaltedHash(password, saltBase64);
        if (exists == null)
        {
            var admin = new User
            {
                Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId(),
                UserName = userName,
                DisplayName = displayName ?? userName,
                Email = email,
                Enabled = true,
                Salt = saltBase64,
                PasswordHash = hash,
                CreatedAt = DateTime.Now,
                IsDeleted = false
            };
            await db.Insertable(admin).ExecuteCommandAsync();
        }
        else
        {
            exists.DisplayName = displayName ?? userName;
            exists.Email = email;
            exists.Enabled = true;
            exists.Salt = saltBase64;
            exists.PasswordHash = hash;
            await db.Updateable(exists).ExecuteCommandAsync();
        }
    }

    /// <summary>
    /// 计算 PBKDF2(SHA256) 哈希，参数与 PasswordHasher 保持一致（Iterations=100000, KeySize=32）。
    /// </summary>
    public static string ComputeSaltedHash(string password, string saltBase64)
    {
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            Convert.FromBase64String(saltBase64),
            100_000,
            HashAlgorithmName.SHA256,
            32);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// 按方言切分 SQL 脚本批次并执行；对每个批次先做方言清洗（仅 SqlServer 用于 RowVersion 移除），
    /// 再尝试 TranslateMySqlDDL（默认恒等，将来 PG/Oracle 可用）。
    /// </summary>
    private static async Task ExecuteSqlScriptAsync(
        ISqlSugarClient db,
        IDatabaseDialect dialect,
        string scriptPath,
        CancellationToken ct,
        List<InstallLog> logs)
    {
        var sql = await File.ReadAllTextAsync(scriptPath, ct);
        IEnumerable<string> batches = dialect.SplitBatches(sql);

        int i = 0;
        foreach (var raw in batches)
        {
            var batch = (raw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(batch)) continue;
            i++;
            try
            {
                // 方言级 INSERT 清洗（SQL Server 移除 RowVersion 列，其它方言恒等返回）
                batch = dialect.SanitizeInsertBatch(batch, db);
                // MySQL → 目标方言转写 hook（内置 MySql/SqlServer 实现返回原文，
                // 未来 PG/Oracle/达梦 等方言可选择在此实现轻量 DDL 转写，
                // 以复用现有 MySQL 脚本作为过渡路径）。
                batch = dialect.TranslateMySqlDDL(batch);
                db.Ado.ExecuteCommand(batch);
            }
            catch (Exception ex)
            {
                // 记录完整异常信息，避免前端显示被截断
                logs.Add(new InstallLog { At = DateTime.Now, Level = "ERROR", Message = $"执行脚本批次#{i}失败:\n{ex}" });
                throw;
            }
        }
    }

    private string? FindResourceFile(string name)
    {
        try
        {
            var localDir = Path.Combine(_env.ContentRootPath, "resource");
            var fallbackDir = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "..", "..", "resource"));
            var c1 = Path.Combine(localDir, name);
            if (File.Exists(c1)) return c1;
            var c2 = Path.Combine(fallbackDir, name);
            if (File.Exists(c2)) return c2;
        }
        catch { /* swallow: best effort */ }
        return null;
    }

    /// <summary>
    /// 生成随机的管理后台路径标识（小写字母+数字）
    /// </summary>
    private static string GenerateRandomSlug(int length = 10)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var bytes = RandomNumberGenerator.GetBytes(length);
        var result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = chars[bytes[i] % chars.Length];
        }
        return new string(result);
    }

    /// <summary>
    /// 更新前端 admin.ts 中的 ADMIN_SLUG 值
    /// </summary>
    private async Task UpdateAdminSlugAsync(string newSlug, List<InstallLog> logs)
    {
        var adminTsPath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "..", "..", "web", "src", "config", "admin.ts"));
        if (!File.Exists(adminTsPath))
        {
            Log(logs, $"未找到 admin.ts: {adminTsPath}，跳过路径更新", "WARN");
            return;
        }
        var content = await File.ReadAllTextAsync(adminTsPath);
        var updated = Regex.Replace(content, @"export const ADMIN_SLUG\s*=\s*'[^']*'", $"export const ADMIN_SLUG = '{newSlug}'");
        await File.WriteAllTextAsync(adminTsPath, updated, new UTF8Encoding(false));
        Log(logs, $"已更新 admin.ts ADMIN_SLUG = '{newSlug}'");
    }
}
