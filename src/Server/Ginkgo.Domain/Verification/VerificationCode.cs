using SqlSugar;
using Ginkgo.Domain.Utils;

namespace Ginkgo.Domain.Verification;

/// <summary>
/// 验证码发送记录实体（审计 + 持久化双重职责）。
/// </summary>
[SugarTable("ginkgo_Sys_VerificationCode", TableDescription = "验证码发送记录表")]
public sealed class VerificationCode
{
	/// <summary>主键（雪花ID）</summary>
	[SugarColumn(IsPrimaryKey = true)]
	public long Id { get; set; } = SnowflakeIdGenerator.NextId();

	/// <summary>关联用户ID（匿名场景可为空）</summary>
	[SugarColumn(IsNullable = true)]
	public long? UserId { get; set; }

	/// <summary>接收目标（邮箱地址或手机号）</summary>
	[SugarColumn(Length = 256, IsNullable = false)]
	public string Target { get; set; } = string.Empty;

	/// <summary>发送渠道：0=邮件 1=短信 2=站内通知</summary>
	[SugarColumn(IsNullable = false)]
	public int Channel { get; set; }

	/// <summary>验证用途：0=找回密码 1=登录验证 2=注册验证 3=绑定邮箱 4=绑定手机 10=危险操作 99=自定义</summary>
	[SugarColumn(IsNullable = false)]
	public int Purpose { get; set; }

	/// <summary>用途中文描述（自定义用途时填写）</summary>
	[SugarColumn(Length = 64, IsNullable = true)]
	public string? PurposeLabel { get; set; }

	/// <summary>验证码哈希（SHA256，不存明文）</summary>
	[SugarColumn(Length = 128, IsNullable = false)]
	public string CodeHash { get; set; } = string.Empty;

	/// <summary>过期时间</summary>
	[SugarColumn(IsNullable = false)]
	public DateTime ExpiresAt { get; set; }

	/// <summary>验证通过时间（NULL=未使用）</summary>
	[SugarColumn(IsNullable = true)]
	public DateTime? VerifiedAt { get; set; }

	/// <summary>已尝试校验次数（防暴力破解）</summary>
	[SugarColumn(IsNullable = false)]
	public int Attempts { get; set; }

	/// <summary>最大允许校验次数</summary>
	[SugarColumn(IsNullable = false)]
	public int MaxAttempts { get; set; } = 5;

	/// <summary>发送时的客户端IP</summary>
	[SugarColumn(Length = 64, IsNullable = true)]
	public string? Ip { get; set; }

	/// <summary>创建时间</summary>
	[SugarColumn(IsNullable = false)]
	public DateTime CreatedAt { get; set; } = DateTime.Now;
}
