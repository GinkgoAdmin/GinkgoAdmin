using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Ginkgo.ServerToolkit.Utils;

public static class SequentialGuid
{
    private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

    public static Guid NewGuid()
    {
        Span<byte> g = stackalloc byte[16];
        Rng.GetBytes(g.Slice(0, 10));
        long millis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Span<byte> ts = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(ts, millis);
        ts.Slice(2, 6).CopyTo(g.Slice(10, 6));
        g[8] = (byte)((g[8] & 0x3F) | 0x80);
        g[7] = (byte)((g[7] & 0x0F) | 0x40);
        return new Guid(g);
    }
}

