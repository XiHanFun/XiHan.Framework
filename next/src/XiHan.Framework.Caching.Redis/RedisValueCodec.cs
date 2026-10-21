// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers.Binary;
using StackExchange.Redis;

namespace XiHan.Framework.Caching.Redis;

/// <summary>
/// 值的线上编码：1 字节头（0 绝对、1 滑动），滑动时跟 8 字节窗口毫秒数（小端），其后是载荷。
/// </summary>
internal static class RedisValueCodec
{
    private const byte Absolute = 0;
    private const byte Sliding = 1;

    public static byte[] Encode(ReadOnlyMemory<byte> payload, TimeSpan? sliding)
    {
        if (sliding is null)
        {
            var absolute = new byte[1 + payload.Length];
            absolute[0] = Absolute;
            payload.Span.CopyTo(absolute.AsSpan(1));
            return absolute;
        }

        var encoded = new byte[1 + sizeof(long) + payload.Length];
        encoded[0] = Sliding;
        BinaryPrimitives.WriteInt64LittleEndian(encoded.AsSpan(1), (long)sliding.Value.TotalMilliseconds);
        payload.Span.CopyTo(encoded.AsSpan(1 + sizeof(long)));
        return encoded;
    }

    public static (ReadOnlyMemory<byte> Payload, TimeSpan? Sliding) Decode(byte[] encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        if (encoded.Length == 0)
        {
            throw new InvalidOperationException("Redis 缓存值为空，不是本 Provider 写入的格式。");
        }

        return encoded[0] switch
        {
            Absolute => (new ReadOnlyMemory<byte>(encoded, 1, encoded.Length - 1), null),
            Sliding when encoded.Length >= 1 + sizeof(long) => (
                new ReadOnlyMemory<byte>(encoded, 1 + sizeof(long), encoded.Length - 1 - sizeof(long)),
                TimeSpan.FromMilliseconds(BinaryPrimitives.ReadInt64LittleEndian(encoded.AsSpan(1)))),
            _ => throw new InvalidOperationException("Redis 缓存值头部不合法，不是本 Provider 写入的格式。"),
        };
    }
}
