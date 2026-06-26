namespace Ginkgo.Api.Modules;

/// <summary>
/// 按当前数据库方言定位插件 server/sql 下的 install.sql。
/// </summary>
public static class ModuleInstallSqlLocator
{
    /// <summary>将 Database:Provider 方言代码映射为 sql 子目录名。</summary>
    public static string MapDialectCodeToSqlFolder(string? dialectCode)
    {
        return (dialectCode ?? "mysql").Trim().ToLowerInvariant() switch
        {
            "mysql" => "mysql",
            "postgresql" => "postgresql",
            "sqlserver" => "mssql",
            _ => (dialectCode ?? "mysql").Trim().ToLowerInvariant()
        };
    }

    /// <summary>
    /// 查找 install.sql：优先方言子目录（如 sql/postgresql/install.sql），再回退根目录与其它子目录。
    /// </summary>
    public static List<string> FindInstallSqlFiles(string serverDir, string? dialectCode = null)
    {
        var result = new List<string>();
        var sqlDir = Path.Combine(serverDir, "sql");
        if (!Directory.Exists(sqlDir))
            return result;

        if (!string.IsNullOrWhiteSpace(dialectCode))
        {
            var dialectInstall = Path.Combine(sqlDir, MapDialectCodeToSqlFolder(dialectCode), "install.sql");
            if (File.Exists(dialectInstall))
            {
                result.Add(dialectInstall);
                return result;
            }
        }

        var rootInstall = Path.Combine(sqlDir, "install.sql");
        if (File.Exists(rootInstall))
        {
            result.Add(rootInstall);
            return result;
        }

        foreach (var subDir in Directory.GetDirectories(sqlDir))
        {
            var subInstall = Path.Combine(subDir, "install.sql");
            if (File.Exists(subInstall))
                result.Add(subInstall);
        }

        return result;
    }

    /// <summary>确定 install.sql 相对 server/ 的路径，用于打包导出时保持目录结构。</summary>
    public static string DetermineInstallSqlRelativePath(string serverDir, string? dialectCode = null)
    {
        var sqlDir = Path.Combine(serverDir, "sql");

        if (!string.IsNullOrWhiteSpace(dialectCode))
        {
            var folder = MapDialectCodeToSqlFolder(dialectCode);
            var dialectInstall = Path.Combine(sqlDir, folder, "install.sql");
            if (File.Exists(dialectInstall))
                return Path.Combine("sql", folder, "install.sql");
        }

        var rootInstall = Path.Combine(sqlDir, "install.sql");
        if (File.Exists(rootInstall))
            return Path.Combine("sql", "install.sql");

        if (Directory.Exists(sqlDir))
        {
            foreach (var subDir in Directory.GetDirectories(sqlDir))
            {
                var subInstall = Path.Combine(subDir, "install.sql");
                if (File.Exists(subInstall))
                    return Path.Combine("sql", Path.GetFileName(subDir), "install.sql");
            }
        }

        if (!string.IsNullOrWhiteSpace(dialectCode))
            return Path.Combine("sql", MapDialectCodeToSqlFolder(dialectCode), "install.sql");

        return Path.Combine("sql", "install.sql");
    }
}
