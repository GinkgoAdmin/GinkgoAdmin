using SqlSugar;

namespace Ginkgo.Domain.Files;

/// <summary>
/// 系统文件元数据。
/// </summary>
[SugarTable("ginkgo_Sys_File", TableDescription = "系统文件表（附件元数据：存储提供者/路径/URL/分类/所有者/哈希等）")]
[SugarIndex("IX_SysFile_Owner_CreatedAt", nameof(OwnerId) + "," + nameof(CreatedAt), OrderByType.Desc)]
[SugarIndex("IX_SysFile_Type", nameof(Type), OrderByType.Asc)]
[SugarIndex("IX_SysFile_Department", nameof(DepartmentId), OrderByType.Asc)]
[SugarIndex("IX_SysFile_Hash", nameof(Hash), OrderByType.Asc)]
[SugarIndex("IX_SysFile_Provider", nameof(StorageProvider), OrderByType.Asc)]
public sealed class SysFile : AuditableEntity
{
    [SugarColumn(Length = 512, IsNullable = false, ColumnDescription = "原始文件名")]
    public string FileName { get; private set; } = string.Empty;

    [SugarColumn(Length = 128, IsNullable = true, ColumnDescription = "内容类型（MIME）")]
    public string? ContentType { get; private set; }

    [SugarColumn(IsNullable = false, ColumnDescription = "文件大小（字节）")]
    public long Size { get; private set; }

    [SugarColumn(Length = 64, IsNullable = true, ColumnDescription = "内容哈希（SHA-256）")]
    public string? Hash { get; private set; }

    [SugarColumn(Length = 64, IsNullable = false, ColumnDescription = "存储提供者标识（Local/OSS/..）")]
    public string StorageProvider { get; private set; } = "Local";

    [SugarColumn(Length = 1024, IsNullable = false, ColumnDescription = "存储路径或对象键（相对路径/Key）")]
    public string StoragePath { get; private set; } = string.Empty;

    [SugarColumn(Length = 2048, IsNullable = true, ColumnDescription = "公网访问URL（可空）")]
    public string? Url { get; private set; }

    // OwnerId 已不再使用，统一以审计字段 CreatedBy 作为创建者标识
    [SugarColumn(IsNullable = true, ColumnDescription = "所有者用户Id（可空，兼容历史数据）")]
    public long? OwnerId { get; private set; }

    [SugarColumn(Length = 512, IsNullable = true, ColumnDescription = "标签（逗号分隔，可空）")]
    public string? Tags { get; private set; }

    [SugarColumn(IsNullable = false, ColumnDescription = "版本号（默认1）")]
    public int Version { get; private set; } = 1;

    /// <summary>
    /// 业务分类（来自字典 category=file 的键），默认 default。
    /// 注意：数据库列名为小写 type，与关键词冲突，映射时指定列名。
    /// </summary>
    [SugarColumn(ColumnName = "type", Length = 64, IsNullable = true, ColumnDescription = "业务分类键（默认default）")]
    public string? Type { get; private set; } = "default";

    /// <summary>
    /// 所属部门，可为空。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "所属部门Id（可空）")]
    public long? DepartmentId { get; private set; }

    // ---------- 领域行为 ----------
    public static SysFile CreateNew(string fileName, string contentType, long size,
        long? createdBy, string? type, string? tags, string? hash)
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("文件名不能为空", nameof(fileName));
        if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));
        return new SysFile
        {
            FileName = fileName,
            ContentType = contentType,
            Size = size,
            Hash = hash,
            OwnerId = createdBy, // 兼容：仍写入 OwnerId 以保留历史列；权限/范围使用 CreatedBy
            CreatedBy = createdBy,
            Type = string.IsNullOrWhiteSpace(type) ? "default" : type,
            Tags = tags,
            Version = 1
        };
    }

    public void AttachStorage(string provider, string path)
    {
        if (string.IsNullOrWhiteSpace(provider)) throw new ArgumentException("存储提供者无效", nameof(provider));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("存储路径无效", nameof(path));
        StorageProvider = provider;
        StoragePath = path;
    }

    public void UpdatePublicUrl(string? url)
    {
        Url = string.IsNullOrWhiteSpace(url) ? null : url;
    }

    public void Retag(string? tags)
    {
        Tags = tags;
    }

    public void Retype(string? type)
    {
        Type = string.IsNullOrWhiteSpace(type) ? Type : type;
    }

    public void MarkDeleted(long? operatorId)
    {
        IsDeleted = true;
        UpdatedBy = operatorId;
        UpdatedAt = DateTime.Now;
    }
}


