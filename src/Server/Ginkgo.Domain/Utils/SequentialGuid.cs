using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Ginkgo.Domain.Utils;

/// <summary>
/// 顺序 GUID 生成器（Sequential-At-End）：
/// - 前 10 字节为随机数，后 6 字节为大端毫秒时间戳；
/// - 兼顾分布式唯一性与按时间递增的插入局部性；
/// - 设置 RFC 4122 标志位（Version=4, Variant=RFC4122）。
/// </summary>
public static class SequentialGuid
{
    private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

    /// <summary>
    /// 生成顺序 GUID（尾部顺序，贴近 SQL Server 的聚集索引插入友好性）。
    /// </summary>
    public static Guid NewGuid()
    {
        Span<byte> g = stackalloc byte[16];
        // 前 10 字节随机
        Rng.GetBytes(g.Slice(0, 10));

        // 后 6 字节为毫秒时间戳（大端）
        long millis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Span<byte> ts = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(ts, millis);
        ts.Slice(2, 6).CopyTo(g.Slice(10, 6));

        // RFC 4122 variant (10xx xxxx)
        g[8] = (byte)((g[8] & 0x3F) | 0x80);
        // Version 4 (0100 xxxx)
        g[7] = (byte)((g[7] & 0x0F) | 0x40);

        return new Guid(g);
    }
}

