using SqlSugar;
using System;
using System.Collections.Generic;


namespace Ginkgo.Domain.Logs;

/// <summary>
/// 操作日志实体（逐步对齐标准审计/软删除规范）。
/// 说明：为保障迁移灰度，暂继承 Entity，并以 IsIgnore 方式预埋审计字段；
/// 数据库列上线后去掉 IsIgnore 即可无缝切换。
/// </summary>
[SugarTable("ginkgo_Sys_OpLog")]
public sealed class OpLog : Entity
{
    // Id 属性、CreatedAt/CreatedBy/UpdatedAt/UpdatedBy/IsDeleted 已在本类中定义
    public long? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public string Result { get; set; } = string.Empty;
    public int? ElapsedMs { get; set; }
    public string? DataJson { get; set; }
    
    /// <summary>
    /// 操作时间（数据库 NOT NULL）
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "操作时间")]
    public DateTime At { get; set; }
    
    public string? ModuleCN { get; set; }
    public string? FeatureCN { get; set; }
    public long? DepartmentId { get; set; }

    // 中文审记串：模块-功能-结果（例如："用户管理-新增-成功"）
    [SugarColumn(Length = 200, IsNullable = true, ColumnDescription = "中文审记串：模块-功能-结果")]
    public string? ReviewCN { get; set; }

    // 标准审计/软删除字段（已落库）
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    /// <summary>
    /// 工厂方法：创建基础操作日志并填充审计字段。
    /// </summary>
    public static OpLog Create(string action, string resource, long? createdBy)
    {
        var now = DateTime.Now;
        return new OpLog
        {
            Action = action?.Trim() ?? string.Empty,
            Resource = resource?.Trim() ?? string.Empty,
            UserId = createdBy,
            CreatedBy = createdBy,
            CreatedAt = now,
            At = now,  // 操作时间（数据库 NOT NULL）
            Result = "OK"
        };
    }

    /// <summary>
    /// 附加请求细节（自动脱敏/裁剪可在基础设施实现中升级）。
    /// </summary>
    public void AttachDetails(string? ip, string? userAgent, object? extra = null)
    {
        Ip = ip;
        try
        {
            var dict = string.IsNullOrWhiteSpace(DataJson)
                ? new Dictionary<string, object?>()
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(DataJson) ?? new Dictionary<string, object?>();
            if (!string.IsNullOrWhiteSpace(userAgent)) dict["userAgent"] = userAgent;
            if (!string.IsNullOrWhiteSpace(ip)) dict["ip"] = ip;
            if (extra != null) dict["extra"] = extra;
            DataJson = System.Text.Json.JsonSerializer.Serialize(dict);
        }
        catch { /* 安全兜底：日志附加失败不应影响主流程 */ }
    }

    public DateTime? DeletedAt { get; set; }
    public long? DeletedBy { get; set; }

}


