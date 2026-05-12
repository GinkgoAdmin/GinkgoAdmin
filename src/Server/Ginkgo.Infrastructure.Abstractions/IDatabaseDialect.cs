// 文件功能说明：
// 数据库方言契约接口。把主框架核心中所有"按数据库类型分流"的逻辑（标识符引用、SQL 函数、
// 信息架构查询、库级运维、连接串规整、脚本批次切分、INSERT 清洗、MySQL→目标方言轻量转写
// 等）全部集中下沉到此接口。每个具体数据库实现一个 IDatabaseDialect，由 IDialectRegistry
// 按 Code 路由。
//
// 接口设计原则：
// 1. Singleton 注册，所有成员保持无状态、线程安全。
// 2. 常量返回值用 get 属性，避免方法调用开销。
// 3. 不在此接口里出现任何业务语义；仅承载基础设施级别的方言差异。
// 4. 后续新增能力优先使用 C# 接口默认实现，避免对已有 Dialect 形成破坏性变更。

using System.Data.Common;
using SqlSugar;

namespace Ginkgo.Infrastructure.Abstractions;

/// <summary>
/// 数据库方言契约。各数据库（MySQL / SQL Server / PostgreSQL / Oracle / 达梦 等）各实现一个。
/// </summary>
public interface IDatabaseDialect
{
    // ================================================================
    // 标识与元数据
    // ================================================================

    /// <summary>方言代码（小写），用于配置匹配。例："mysql"、"sqlserver"、"postgresql"。</summary>
    string Code { get; }

    /// <summary>展示名，用于安装向导 / 管理后台 UI。例："MySQL"、"SQL Server"。</summary>
    string DisplayName { get; }

    /// <summary>对应的 SqlSugar.DbType 枚举值。</summary>
    DbType SqlSugarDbType { get; }

    /// <summary>数据库默认监听端口（字符串形式，前端直接绑定）。</summary>
    string DefaultPort { get; }

    /// <summary>方言能力清单（递归 CTE / JSON / 全文检索 等）。</summary>
    DialectCapabilities Capabilities { get; }

    /// <summary>展示用描述符，由 IDialectRegistry.List() 汇总后给前端。</summary>
    DialectDescriptor Descriptor { get; }

    // ================================================================
    // 标识符与字面量
    // ================================================================

    /// <summary>转义标识符（列名/表名）。MySQL 用反引号、SQL Server 用中括号、PG/Oracle 用双引号。</summary>
    string QuoteIdentifier(string name);

    /// <summary>
    /// 转义表名（带可选 schema）。schema 为 null 时不附加 schema 限定。
    /// </summary>
    string QuoteTable(string? schema, string name);

    /// <summary>参数前缀。MySQL/SQL Server 用 "@"，PostgreSQL/Oracle 用 ":"。</summary>
    string ParameterPrefix { get; }

    /// <summary>布尔字面量。MySQL 用 1/0，SQL Server 用 1/0，PG 用 TRUE/FALSE。</summary>
    string BoolLiteral(bool value);

    // ================================================================
    // 函数与表达式
    // ================================================================

    /// <summary>
    /// "当前 UTC 时间" 的 SQL 表达式。
    /// MySQL = UTC_TIMESTAMP()，SQL Server = GETUTCDATE()，PG = (NOW() AT TIME ZONE 'UTC')。
    /// </summary>
    string UtcNowExpr { get; }

    /// <summary>
    /// "当前服务器本地时间" 的 SQL 表达式。
    /// MySQL = NOW()，SQL Server = GETDATE()，PG = NOW()。
    /// </summary>
    string NowExpr { get; }

    /// <summary>
    /// 构建分页/限制子句。
    /// MySQL = "LIMIT {offset}, {count}" / "LIMIT {count}"；
    /// SQL Server 2012+ = "OFFSET {offset} ROWS FETCH NEXT {count} ROWS ONLY"；
    /// PG = "LIMIT {count} OFFSET {offset}"。
    /// 返回空字符串表示无 LIMIT。
    /// </summary>
    string BuildLimitClause(int? offset, int? count);

    // ================================================================
    // 信息架构（元查询）
    // ================================================================

    /// <summary>
    /// 列出当前数据库中以 @prefix 开头的所有表名。SQL 中必须用 @prefix 参数（即使方言用 ":"，
    /// 也由 dialect 实现内部转换；调用方统一传 "@prefix" 风格的 SugarParameter 即可）。
    /// </summary>
    string SqlListTablesByPrefix { get; }

    /// <summary>
    /// 查询指定表的主键列名清单。需要 @table 参数。
    /// </summary>
    string SqlGetPrimaryKeyColumns { get; }

    /// <summary>
    /// 查询指定表的所有列与类型（有序）。需要 @schema 与 @table 参数；返回列 (Name, Type) 顺序与表定义一致。
    /// </summary>
    string SqlGetColumnsWithTypes { get; }

    /// <summary>
    /// 查询指定表的 RowVersion/timestamp 列集合。仅 SQL Server 返回非空；其它方言返回空 SQL 或 null。
    /// </summary>
    string? SqlGetRowVersionColumns { get; }

    // ================================================================
    // 库级运维（异步）
    // ================================================================

    /// <summary>
    /// 检查指定数据库是否已存在。<paramref name="masterConnectionString"/> 为去掉 Database/Initial Catalog
    /// 后的"主连接串"（MySQL 可保留 Database=，SqlServer 必须切换到 master）。
    /// </summary>
    Task<bool> DatabaseExistsAsync(string masterConnectionString, string dbName, CancellationToken ct);

    /// <summary>
    /// 创建数据库（若不存在则创建）。返回 true 表示本次新建；false 表示原已存在。
    /// </summary>
    Task<bool> CreateDatabaseIfNotExistsAsync(string masterConnectionString, string dbName, CancellationToken ct);

    /// <summary>
    /// 删除数据库（不存在则忽略）。仅在安装失败回滚时调用。
    /// </summary>
    Task DropDatabaseAsync(string masterConnectionString, string dbName, CancellationToken ct);

    // ================================================================
    // 连接串
    // ================================================================

    /// <summary>
    /// 规整化连接串：补齐方言推荐的默认参数（如 MySQL 的 CharSet=utf8mb4、SQL Server 的
    /// MultipleActiveResultSets=true 等）。输入异常时返回原文，不抛异常。
    /// </summary>
    string NormalizeConnectionString(string connectionString);

    /// <summary>
    /// 根据 host/port/user/password 构建用于"测试连接"的连接串（不指定具体数据库）。
    /// </summary>
    string BuildTestConnectionString(string server, string? port, string user, string password);

    /// <summary>
    /// 从完整连接串中解析数据库名（兼容 Database=/Initial Catalog=）。找不到返回 null。
    /// </summary>
    string? TryGetDatabaseName(string connectionString);

    // ================================================================
    // 脚本资源与批次切分
    // ================================================================

    /// <summary>主框架核心安装脚本资源文件名（如 "mysql_install.sql"）。</summary>
    string InstallScriptResourceName { get; }

    /// <summary>主框架核心菜单初始化脚本资源文件名（如 "mysql_init_menus.sql"）。</summary>
    string InitMenusScriptResourceName { get; }

    /// <summary>
    /// 将完整 SQL 文本按方言切分为多个可独立执行的批次。
    /// MySQL 按 ;（忽略引号内）；SQL Server 按 GO；PG/Oracle 可能需要识别 $$/; 与匿名块。
    /// </summary>
    IEnumerable<string> SplitBatches(string sql);

    // ================================================================
    // 安装期 INSERT 批次清洗（默认无操作）
    // ================================================================

    /// <summary>
    /// 安装期对 INSERT 批次做方言相关清洗。仅 SQL Server 实现移除 rowversion 列的逻辑；
    /// 其它方言默认返回原文。
    /// </summary>
    string SanitizeInsertBatch(string batch, ISqlSugarClient sugar);

    // ================================================================
    // MySQL → 当前方言的轻量 DDL 转写 hook（默认恒等）
    // ================================================================

    /// <summary>
    /// 当模块仅提供 MySQL 方言的 install.sql 时，调用此方法尝试转写为当前方言。
    /// 默认实现返回原文（MySQL 自身、未实现转写的方言均如此）。
    /// 仅适合简单 DDL（CREATE TABLE / INDEX / 简单 INSERT），不适合存储过程/触发器/JSON 函数。
    /// </summary>
    string TranslateMySqlDDL(string mysqlDdl);

    // ================================================================
    // 工具：执行 SqlSugar 底层 ADO.NET 命令（避免实现层重复造轮子）
    // ================================================================

    /// <summary>
    /// 创建底层 DbConnection（用于库级运维场景，绕过 SqlSugar 自带的 ISqlSugarClient）。
    /// 默认实现通常不需要覆写。
    /// </summary>
    DbConnection CreateConnection(string connectionString);
}
