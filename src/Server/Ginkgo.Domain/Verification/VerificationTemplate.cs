using SqlSugar;
using Ginkgo.Domain.Utils;

namespace Ginkgo.Domain.Verification;

/// <summary>
/// 验证码消息模板实体（邮件/短信模板可后台配置）。
/// </summary>
[SugarTable("ginkgo_Sys_VerificationTemplate", TableDescription = "验证码消息模板表")]
public sealed class VerificationTemplate
{
	/// <summary>主键（雪花ID）</summary>
	[SugarColumn(IsPrimaryKey = true)]
	public long Id { get; set; } = SnowflakeIdGenerator.NextId();

	/// <summary>验证用途（与 VerificationCode 表一致）</summary>
	[SugarColumn(IsNullable = false, ColumnDataType = "smallint")]
	public int Purpose { get; set; }

	/// <summary>渠道：0=邮件 1=短信</summary>
	[SugarColumn(IsNullable = false, ColumnDataType = "smallint")]
	public int Channel { get; set; }

	/// <summary>模板名称（如：找回密码邮件模板）</summary>
	[SugarColumn(Length = 64, IsNullable = false)]
	public string Name { get; set; } = string.Empty;

	/// <summary>邮件主题（短信渠道可为空）</summary>
	[SugarColumn(Length = 256, IsNullable = true)]
	public string? Subject { get; set; }

	/// <summary>
	/// 模板正文（支持占位符：{code}=验证码, {minutes}=有效分钟数, {purpose}=用途描述, {appName}=应用名称）
	/// </summary>
	[SugarColumn(ColumnDataType = "text", IsNullable = false)]
	public string BodyTemplate { get; set; } = string.Empty;

	/// <summary>是否为HTML格式（邮件用）</summary>
	[SugarColumn(IsNullable = false)]
	public bool IsHtml { get; set; } = true;

	/// <summary>是否为该用途的默认模板</summary>
	[SugarColumn(IsNullable = false)]
	public bool IsDefault { get; set; }

	/// <summary>是否启用</summary>
	[SugarColumn(IsNullable = false)]
	public bool Enabled { get; set; } = true;

	/// <summary>排序</summary>
	[SugarColumn(IsNullable = false)]
	public int SortOrder { get; set; }

	/// <summary>创建时间</summary>
	[SugarColumn(IsNullable = false)]
	public DateTime CreatedAt { get; set; } = DateTime.Now;

	/// <summary>更新时间</summary>
	[SugarColumn(IsNullable = true)]
	public DateTime? UpdatedAt { get; set; }
}
