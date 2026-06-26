namespace Ginkgo.Application.Settings;

/// <summary>
/// 
/// 系统配置 DTO。
/// </summary>
public sealed class SettingDto
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
    public string? Class { get; set; }
    public int? Version { get; set; }
    /// <summary>所属模块标识：sys=主框架，其他为插件 ModuleId。</summary>
    public string? Module { get; set; }
}

