// 文件功能说明：
// 通用敏感字段保护器抽象。用于对租户连接串、第三方 API 密钥等敏感字段做对称加密。
// 默认实现位于 Ginkgo.Infrastructure.Security.AesGcmSecretProtector（AES-256-GCM）。
//
// 设计要点：
// - 密钥来源优先级：环境变量 GINKGO_TENANT_DB_KEY > appsettings.json:Database:TenantDbKey > 启动随机串（仅 dev，
//   该模式下应用重启会导致历史密文无法解出，需在生产环境强制配置）。
// - 加密格式：Base64( 12B Nonce | 16B Tag | Ciphertext )。同一明文每次加密结果不同，符合 GCM 语义。
// - 不在框架内提供"轮换密钥"机制；如需轮换由业务侧自行配合工具迁移。

namespace Ginkgo.Infrastructure.Abstractions;

/// <summary>
/// 对称加密敏感字段保护器。供租户连接串、第三方密钥等需要"密文落库 / 明文使用"的场景使用。
/// </summary>
public interface IConnectionSecretProtector
{
    /// <summary>
    /// 将明文加密为 Base64 字符串（含 Nonce/Tag）。返回值可直接落库。
    /// </summary>
    /// <param name="plaintext">明文。null/空串原样返回。</param>
    string Protect(string? plaintext);

    /// <summary>
    /// 将 <see cref="Protect"/> 产出的密文还原为明文。已经是明文（未带 Nonce/Tag）时原样返回，兼容历史数据。
    /// </summary>
    /// <param name="protectedText">密文。null/空串原样返回。</param>
    string Unprotect(string? protectedText);

    /// <summary>
    /// 判断给定字符串是否为本保护器产出的密文格式。仅供调试/迁移用，不作为安全边界。
    /// </summary>
    bool IsProtected(string? text);
}
