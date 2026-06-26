namespace Ginkgo.Api.Modules;

/// <summary>
/// 当前数据库方言所需的安装 SQL 在插件包中不存在时抛出。
/// </summary>
public sealed class ModuleInstallSqlNotFoundException : Exception
{
    public ModuleInstallSqlNotFoundException(string dialectCode, string dialectFolder, IEnumerable<string> missingPaths)
        : base(BuildMessage(dialectCode, dialectFolder, missingPaths))
    {
        DialectCode = dialectCode;
        DialectFolder = dialectFolder;
        MissingPaths = missingPaths.ToList();
    }

    public string DialectCode { get; }
    public string DialectFolder { get; }
    public IReadOnlyList<string> MissingPaths { get; }

    private static string BuildMessage(string dialectCode, string dialectFolder, IEnumerable<string> missingPaths)
    {
        var missing = string.Join(", ", missingPaths);
        return $"当前数据库类型为「{dialectCode}」，但插件包中未找到对应的安装 SQL（期望目录 sql/{dialectFolder}/，缺失: {missing}）。" +
               "请使用打包时勾选该数据库类型后重新打包，或手动补齐对应方言的 install.sql / init_data.sql。";
    }
}
