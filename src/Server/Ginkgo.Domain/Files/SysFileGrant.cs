using System.Security.Cryptography;
using SqlSugar;

namespace Ginkgo.Domain.Files;

/// <summary>
/// 文件公开访问授权。
/// 当某个业务对象（如系统设置、公开门户）需要公开展示某文件时，
/// 由该业务对象创建 Grant，生成高强度随机 GrantKey 作为匿名访问凭据。
/// </summary>
[SugarTable("ginkgo_Sys_FileGrant", TableDescription = "文件公开访问授权表")]
[SugarIndex("IX_FileGrant_GrantKey", nameof(GrantKey), OrderByType.Asc, true)]
[SugarIndex("IX_FileGrant_FileId", nameof(FileId), OrderByType.Asc)]
[SugarIndex("IX_FileGrant_Ref", nameof(RefType) + "," + nameof(RefId) + "," + nameof(FieldName), OrderByType.Asc)]
public sealed class SysFileGrant : AuditableEntity
{
    /// <summary>
    /// 关联的文件 Id。
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "关联文件Id")]
    public long FileId { get; private set; }

    /// <summary>
    /// 公开访问密钥（高强度随机串，Base64Url 编码，不可从 FileId 派生）。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = false, ColumnDescription = "公开访问密钥（随机串）")]
    public string GrantKey { get; private set; } = string.Empty;

    /// <summary>
    /// 引用方类型（如 Setting、Article、Portal 等）。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = false, ColumnDescription = "引用方类型")]
    public string RefType { get; private set; } = string.Empty;

    /// <summary>
    /// 引用方业务对象标识（如 Site.Logo、文章Id 等）。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = false, ColumnDescription = "引用方对象标识")]
    public string RefId { get; private set; } = string.Empty;

    /// <summary>
    /// 引用方字段名（如 Logo、CoverImage 等）。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = true, ColumnDescription = "引用方字段名")]
    public string? FieldName { get; private set; }

    /// <summary>
    /// 是否已撤销。
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "是否已撤销")]
    public bool IsRevoked { get; private set; }

    /// <summary>
    /// 过期时间（null 表示永不过期）。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "过期时间（null=永不过期）")]
    public DateTime? ExpiresAt { get; private set; }

    // ---------- 领域行为 ----------

    /// <summary>
    /// 创建新的文件公开授权。
    /// </summary>
    public static SysFileGrant Create(long fileId, string refType, string refId,
        string? fieldName, DateTime? expiresAt = null, long? operatorId = null)
    {
        if (fileId <= 0) throw new ArgumentException("文件Id无效", nameof(fileId));
        if (string.IsNullOrWhiteSpace(refType)) throw new ArgumentException("引用类型不能为空", nameof(refType));
        if (string.IsNullOrWhiteSpace(refId)) throw new ArgumentException("引用标识不能为空", nameof(refId));

        return new SysFileGrant
        {
            FileId = fileId,
            GrantKey = GenerateGrantKey(),
            RefType = refType.Trim(),
            RefId = refId.Trim(),
            FieldName = string.IsNullOrWhiteSpace(fieldName) ? null : fieldName.Trim(),
            IsRevoked = false,
            ExpiresAt = expiresAt,
            CreatedBy = operatorId
        };
    }

    /// <summary>
    /// 撤销此授权，公开链接立即失效。
    /// </summary>
    public void Revoke(long? operatorId = null)
    {
        IsRevoked = true;
        UpdatedBy = operatorId;
        UpdatedAt = DateTime.Now;
    }

    /// <summary>
    /// 判断当前授权是否有效（未撤销且未过期）。
    /// </summary>
    public bool IsValid()
    {
        if (IsRevoked) return false;
        if (IsDeleted) return false;
        if (ExpiresAt.HasValue && ExpiresAt.Value < DateTime.Now) return false;
        return true;
    }

    // ---------- 私有方法 ----------

    /// <summary>
    /// 生成 32 字节的高强度随机 Base64Url 串作为 GrantKey。
    /// </summary>
    private static string GenerateGrantKey()
    {
        var data = new byte[32];
        RandomNumberGenerator.Fill(data);
        // Base64Url 编码：去 padding，替换 +/ 为 -_
        var s = Convert.ToBase64String(data);
        s = s.Split('=')[0];
        s = s.Replace('+', '-');
        s = s.Replace('/', '_');
        return s;
    }
}
