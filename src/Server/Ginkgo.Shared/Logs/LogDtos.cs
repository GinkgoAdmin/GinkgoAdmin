using Ginkgo.Shared;
using System.ComponentModel.DataAnnotations;

namespace Ginkgo.Application.Logs;

/// <summary>
/// 追加操作日志输入。
/// </summary>
public sealed class AppendOpLogInput
{
    [Required] public string Action { get; set; } = string.Empty;
    [Required] public string Resource { get; set; } = string.Empty;
    public string? Result { get; set; } = "OK";
    public int? ElapsedMs { get; set; }
    public string? DataJson { get; set; }
    public long? DepartmentId { get; set; }

    public string? Ip { get; set; }
    public string? UserAgent { get; set; }

    // 标准审计字段（可选输入；未提供时由应用层自动填充）
    public DateTime? CreatedAt { get; set; }
    public long? CreatedBy { get; set; }

    // 可选：模块/功能中文，及审记串（模块-功能-结果）
    public string? ModuleCN { get; set; }
    public string? FeatureCN { get; set; }
    public string? ReviewCN { get; set; }
}

/// <summary>
/// 查询操作日志输入。
/// </summary>
public sealed class ListOpLogsInput
{
    // 分页参数（PageRequest 为 sealed，故此处内联定义）
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public long? UserId { get; set; }
    public long? DepartmentId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Action { get; set; }
    public string? Resource { get; set; }
    // 统一关键字（支持按用户 UserName/DisplayName/Email/Phone 模糊匹配）
    public string? Keyword { get; set; }
    // 模块名模糊（映射到 ModuleCN）
    public string? Module { get; set; }
    // 功能名模糊（映射到 FeatureCN）
    public string? Feature { get; set; }
    // 结果类型（normal/error/unknown）
    public string? Type { get; set; }
}

/// <summary>
/// 操作日志列表项。
/// </summary>
public sealed class OpLogListItemDto
{
    public long Id { get; set; }
    public long? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public string Result { get; set; } = string.Empty;
    public int? ElapsedMs { get; set; }
    public string? DataJson { get; set; }
    public string? ModuleCN { get; set; }
    public string? FeatureCN { get; set; }
    public long? DepartmentId { get; set; }
    public string? ReviewCN { get; set; }
    
    /// <summary>
    /// 操作时间（兼容旧版 WPF 客户端）
    /// </summary>
    public DateTime At { get; set; }

    // 新增：标准审计与软删除字段（Controller 可按需选择返回以保持兼容）
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public long? DeletedBy { get; set; }

    // 便于前端显示的扩展字段（来自用户表）
    public string? UserName { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
