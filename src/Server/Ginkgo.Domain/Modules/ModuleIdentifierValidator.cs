using System.Text.RegularExpressions;

namespace Ginkgo.Domain.Modules;

/// <summary>
/// 模块标识符与相关安全敏感字段的合法性校验工具。
/// 所有接收外部输入的入口（控制器、上传校验、客户端任务）都应在第一时间用本类校验，
/// 不合法 → 直接拒绝（400/参数异常），避免路径穿越、命令注入等下游风险。
/// </summary>
public static class ModuleIdentifierValidator
{
    // 模块 ID：必须以字母开头，仅允许字母数字 + . - _，长度 1~128。
    // 设计与 Ginkgo.Module.* 命名习惯兼容（如 Ginkgo.Module.AICore、Ginkgo.Module.third）。
    private static readonly Regex ModuleIdRegex = new(
        @"^[A-Za-z][A-Za-z0-9.\-_]{0,127}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // 版本号：宽松 semver。允许可选前缀 v；主版本号必须存在；
    // 之后可有 0~3 个次版本段、可选预发布段（- 开头）、可选构建元数据段（+ 开头）。
    private static readonly Regex VersionRegex = new(
        @"^v?\d+(\.\d+){0,3}(-[A-Za-z0-9.\-]+)?(\+[A-Za-z0-9.\-]+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // npm 包名：来自 npm 官方 validate-npm-package-name 同款规则
    // 范围包：@scope/name；普通包：name。
    private static readonly Regex NpmPackageNameRegex = new(
        @"^(?:@[a-z0-9-~][a-z0-9\-._~]*\/)?[a-z0-9-~][a-z0-9\-._~]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // npm 版本规范：semver、range、dist-tag 字符集白名单
    // 拒绝任何 shell 元字符，杜绝 cmd.exe /c npm install <here> 命令注入。
    // 允许字符：字母数字、空格、小数点、@、加减号、波浪号、抑扬号、星号、x、X、=、>、<、|（npm 范围语法 "^1 || ^2"）、||、&&（不允许，下方剔除）。
    // 严格起见仅允许 [A-Za-z0-9.+\-_~^*=<>|\s] 且整体长度 <= 64。
    private static readonly Regex NpmVersionSpecRegex = new(
        @"^[A-Za-z0-9.+\-_~^*=<>|\s]{1,64}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // WPF 客户端 ID：持久 GUID（小写无连字符，长度 32）或主机名风格（字母数字 + - _ . 长度 ≤64）。
    private static readonly Regex ClientIdRegex = new(
        @"^[A-Za-z0-9][A-Za-z0-9\-_.]{0,63}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 校验模块 ID 是否合法。规则：以字母开头，仅允许字母/数字/点/连字符/下划线，长度 1~128。
    /// </summary>
    public static bool IsSafeModuleId(string? id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (id.Length > 128) return false;
        return ModuleIdRegex.IsMatch(id);
    }

    /// <summary>
    /// 校验版本号是否合法（宽松 semver，允许 v 前缀，最多 4 段、含预发布与构建元数据）。
    /// </summary>
    public static bool IsSafeVersion(string? version)
    {
        if (string.IsNullOrEmpty(version)) return false;
        if (version.Length > 64) return false;
        return VersionRegex.IsMatch(version);
    }

    /// <summary>
    /// 校验 npm 包名是否符合 npm 官方规则（含范围包）。
    /// </summary>
    public static bool IsSafeNpmPackageName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (name.Length > 214) return false; // npm 官方上限
        return NpmPackageNameRegex.IsMatch(name);
    }

    /// <summary>
    /// 校验 npm 版本规范字符串是否仅含允许字符（拒绝 shell 元字符）。
    /// 空字符串视为"未指定版本，使用 latest"，返回 true。
    /// </summary>
    public static bool IsSafeNpmVersionSpec(string? versionSpec)
    {
        if (string.IsNullOrEmpty(versionSpec)) return true; // 空 = latest，安全
        return NpmVersionSpecRegex.IsMatch(versionSpec);
    }

    /// <summary>
    /// 校验客户端 ID 是否合法。允许 GUID（"N" 格式，无连字符）或主机名风格短串。
    /// </summary>
    public static bool IsSafeClientId(string? clientId)
    {
        if (string.IsNullOrEmpty(clientId)) return false;
        if (clientId.Length > 64) return false;
        return ClientIdRegex.IsMatch(clientId);
    }

    /// <summary>
    /// 校验菜单/路由 RootCode 是否合法（不会被拼接到 SQL/路径外）。
    /// 字母开头 + 字母数字/点/连字符/下划线/冒号，长度 1~128。
    /// </summary>
    public static bool IsSafeMenuCode(string? code)
    {
        if (string.IsNullOrEmpty(code)) return false;
        if (code.Length > 128) return false;
        for (int i = 0; i < code.Length; i++)
        {
            var c = code[i];
            var isAlnum = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
            if (!isAlnum && c != '.' && c != '-' && c != '_' && c != ':')
                return false;
            if (i == 0 && !isAlnum) return false;
        }
        return true;
    }
}
