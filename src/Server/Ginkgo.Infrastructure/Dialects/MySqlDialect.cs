// 文件功能说明：
// MySQL 方言实现。
// 把原先散落在 Ginkgo.Api.Install.InstallerService、Ginkgo.Api.Modules.ModuleSqlExecutor 等
// 处的 MySQL 特定逻辑（反引号、UTC_TIMESTAMP、CharSet=utf8mb4、按 ; 切批 等）集中到此处。

using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;
using Ginkgo.Infrastructure.Abstractions;
using MySqlConnector;
using SqlSugar;

namespace Ginkgo.Infrastructure.Dialects;

/// <summary>
/// MySQL 数据库方言实现。
/// </summary>
public sealed class MySqlDialect : IDatabaseDialect
{
    /// <inheritdoc/>
    public string Code => "mysql";

    /// <inheritdoc/>
    public string DisplayName => "MySQL";

    /// <inheritdoc/>
    public DbType SqlSugarDbType => DbType.MySql;

    /// <inheritdoc/>
    public string DefaultPort => "3306";

    /// <inheritdoc/>
    public DialectCapabilities Capabilities { get; } = new(
        SupportsRecursiveCte: false, // MySQL 5.7 不支持递归 CTE，框架仍以兼容 5.7+ 为底线
        SupportsWindowFunctions: false, // MySQL 5.7 不支持，8+ 才支持
        SupportsJsonFunctions: true, // MySQL 5.7+ 支持 JSON_EXTRACT 等
        SupportsMergeStatement: false,
        SupportsArrayType: false,
        SupportsFullTextSearch: true, // MATCH AGAINST
        SupportsMultipleActiveResultSets: false,
        NeedsRowVersionInsertSanitization: false);

    /// <inheritdoc/>
    public DialectDescriptor Descriptor => new(
        Code: Code,
        DisplayName: DisplayName,
        DefaultPort: DefaultPort,
        ConnectionStringTemplate: "Server={Server};Port={Port};Database={Database};User Id={User};Password={Password};CharSet=utf8mb4;Allow User Variables=true;");

    // ================================================================
    // 标识符与字面量
    // ================================================================

    /// <inheritdoc/>
    public string QuoteIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;
        if (name.Length >= 2 && name[0] == '`' && name[^1] == '`') return name;
        // 仅做基础转义：把反引号替换为双反引号，避免注入
        return "`" + name.Replace("`", "``") + "`";
    }

    /// <inheritdoc/>
    public string QuoteTable(string? schema, string name)
        => string.IsNullOrWhiteSpace(schema)
            ? QuoteIdentifier(name)
            : $"{QuoteIdentifier(schema!)}.{QuoteIdentifier(name)}";

    /// <inheritdoc/>
    public string ParameterPrefix => "@";

    /// <inheritdoc/>
    public string BoolLiteral(bool value) => value ? "1" : "0";

    // ================================================================
    // 函数与表达式
    // ================================================================

    /// <inheritdoc/>
    public string UtcNowExpr => "UTC_TIMESTAMP()";

    /// <inheritdoc/>
    public string NowExpr => "NOW()";

    /// <inheritdoc/>
    public string BuildLimitClause(int? offset, int? count)
    {
        if (count is null) return string.Empty;
        if (offset is null || offset == 0) return $"LIMIT {count}";
        return $"LIMIT {offset}, {count}";
    }

    // ================================================================
    // 信息架构（元查询）
    // ================================================================

    /// <inheritdoc/>
    public string SqlListTablesByPrefix =>
        "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES " +
        "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME LIKE @prefix";

    /// <inheritdoc/>
    public string SqlGetPrimaryKeyColumns =>
        "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE " +
        "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND CONSTRAINT_NAME = 'PRIMARY' " +
        "ORDER BY ORDINAL_POSITION";

    /// <inheritdoc/>
    public string SqlGetColumnsWithTypes =>
        "SELECT COLUMN_NAME, COLUMN_TYPE FROM INFORMATION_SCHEMA.COLUMNS " +
        "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table " +
        "ORDER BY ORDINAL_POSITION";

    /// <inheritdoc/>
    public string? SqlGetRowVersionColumns => null; // MySQL 无 rowversion 概念

    // ================================================================
    // 库级运维
    // ================================================================

    /// <inheritdoc/>
    public async Task<bool> DatabaseExistsAsync(string masterConnectionString, string dbName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dbName)) return false;
        try
        {
            // MySQL 连接 master 库不存在的概念，连无 Database 也行；这里去掉 Database 参数
            var csb = new MySqlConnectionStringBuilder(masterConnectionString) { Database = string.Empty };
            using var conn = new MySqlConnection(csb.ConnectionString);
            await conn.OpenAsync(ct);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = @n";
            cmd.Parameters.Add(new MySqlParameter("@n", dbName));
            var r = await cmd.ExecuteScalarAsync(ct);
            return r != null;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> CreateDatabaseIfNotExistsAsync(string masterConnectionString, string dbName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dbName))
            throw new InvalidOperationException("无法从连接字符串解析数据库名");
        if (await DatabaseExistsAsync(masterConnectionString, dbName, ct))
            return false;

        var csb = new MySqlConnectionStringBuilder(masterConnectionString) { Database = string.Empty };
        using var conn = new MySqlConnection(csb.ConnectionString);
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        // 显式指定字符集与排序，保持与 mysql_install.sql 一致
        cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS {QuoteIdentifier(dbName)} DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    /// <inheritdoc/>
    public async Task DropDatabaseAsync(string masterConnectionString, string dbName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dbName)) return;
        try
        {
            var csb = new MySqlConnectionStringBuilder(masterConnectionString) { Database = string.Empty };
            using var conn = new MySqlConnection(csb.ConnectionString);
            await conn.OpenAsync(ct);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP DATABASE IF EXISTS {QuoteIdentifier(dbName)}";
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            // 删除失败仅吞，调用方会记录日志
        }
    }

    // ================================================================
    // 连接串
    // ================================================================

    /// <inheritdoc/>
    public string NormalizeConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return connectionString;
        try
        {
            var csb = new MySqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrEmpty(csb.CharacterSet))
                csb.CharacterSet = "utf8mb4";
            // 模块安装/升级 SQL 普遍使用 @col_exists + PREPARE 做幂等 ALTER；
            // MySqlConnector 默认把 @xxx 当参数占位符，必须显式允许用户变量。
            csb.AllowUserVariables = true;
            return csb.ConnectionString;
        }
        catch
        {
            return connectionString;
        }
    }

    /// <inheritdoc/>
    public string BuildTestConnectionString(string server, string? port, string user, string password)
    {
        var p = string.IsNullOrWhiteSpace(port) ? DefaultPort : port;
        return $"Server={server};Port={p};User Id={user};Password={password};";
    }

    /// <inheritdoc/>
    public string? TryGetDatabaseName(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        var m = Regex.Match(connectionString, @"(?i)(?:Initial Catalog|Database)\s*=\s*([^;]+)");
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    // ================================================================
    // 脚本资源与批次切分
    // ================================================================

    /// <inheritdoc/>
    public string InstallScriptResourceName => "mysql_install.sql";

    /// <inheritdoc/>
    public string InitMenusScriptResourceName => "mysql_init_menus.sql";

    /// <inheritdoc/>
    public IEnumerable<string> SplitBatches(string sql)
    {
        // 按 ; 切分（忽略字符串内的 ;）。保持与原 InstallerService.SplitMySqlStatements 行为一致。
        var list = new List<string>();
        var sb = new StringBuilder();
        bool inString = false; char quote = '\0'; bool escape = false;
        for (int idx = 0; idx < sql.Length; idx++)
        {
            char c = sql[idx];
            if (inString)
            {
                sb.Append(c);
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == quote) { inString = false; quote = '\0'; }
                continue;
            }
            if (c == '\'' || c == '"')
            {
                inString = true; quote = c; sb.Append(c); continue;
            }
            if (c == ';')
            {
                list.Add(sb.ToString());
                sb.Clear();
                continue;
            }
            sb.Append(c);
        }
        if (sb.Length > 0) list.Add(sb.ToString());
        return list;
    }

    // ================================================================
    // 安装期 INSERT 清洗（MySQL 无需）
    // ================================================================

    /// <inheritdoc/>
    public string SanitizeInsertBatch(string batch, ISqlSugarClient sugar) => batch;

    /// <inheritdoc/>
    public string ToIdempotentInsert(string insertStatement)
    {
        if (string.IsNullOrWhiteSpace(insertStatement)) return insertStatement;
        // 仅改写以 INSERT INTO 开头的语句（忽略前导空白与大小写），改为 INSERT IGNORE INTO。
        // 已经是 INSERT IGNORE / INSERT ... ON DUPLICATE 的语句不做处理，避免重复改写。
        return Regex.Replace(
            insertStatement,
            @"^(\s*)INSERT\s+INTO\b",
            "$1INSERT IGNORE INTO",
            RegexOptions.IgnoreCase);
    }

    // ================================================================
    // MySQL → 当前方言转写（MySQL 自身：恒等）
    // ================================================================

    /// <inheritdoc/>
    public string TranslateMySqlDDL(string mysqlDdl) => mysqlDdl;

    // ================================================================
    // 工具
    // ================================================================

    /// <inheritdoc/>
    public DbConnection CreateConnection(string connectionString) => new MySqlConnection(connectionString);
}
