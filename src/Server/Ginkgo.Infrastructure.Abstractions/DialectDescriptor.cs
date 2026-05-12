// 文件功能说明：
// 数据库方言"描述符"，用于对外（如安装向导 UI / API）暴露当前所有可用方言的元数据。
// 与 IDatabaseDialect 区分：DialectDescriptor 只承载展示用元数据，不承载执行能力。

namespace Ginkgo.Infrastructure.Abstractions;

/// <summary>
/// 数据库方言描述符，仅供 UI/前端展示与连接串模板生成使用。
/// </summary>
/// <param name="Code">方言代码（小写），如 "mysql" / "sqlserver" / "postgresql"。与 Database:Provider 配置值匹配（大小写不敏感）。</param>
/// <param name="DisplayName">展示名，如 "MySQL" / "SQL Server" / "PostgreSQL"。</param>
/// <param name="DefaultPort">数据库默认端口（字符串形式，便于前端直接绑定输入框）。</param>
/// <param name="ConnectionStringTemplate">连接串模板，占位符使用 {Server}、{Port}、{Database}、{User}、{Password}。</param>
public sealed record DialectDescriptor(
    string Code,
    string DisplayName,
    string DefaultPort,
    string ConnectionStringTemplate);
