// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers.Binary;

namespace XiHan.Framework.Caching.Redis;

/// <summary>
/// 值的线上编码：1 字节头（0 绝对、1 滑动），滑动时跟 8 字节窗口毫秒数（小端），其后是载荷。
/// </summary>
internal static class RedisValueCodec
{
    /// <summary>
    /// 头部标记：绝对过期，后面直接是载荷。
    /// </summary>
    private const byte Absolute = 0;

    /// <summary>
    /// 头部标记：滑动过期，后面先是 8 字节窗口毫秒数，再是载荷。
    /// </summary>
    private const byte Sliding = 1;

    /// <summary>
    /// 编码：给载荷加上头部；<paramref name="sliding"/> 不为 <see langword="null"/> 时一并写入窗口。
    /// </summary>
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

    /// <summary>
    /// 解码：按头部拆出载荷与滑动窗口；不是本 Provider 写入的格式抛出 <see cref="InvalidOperationException"/>。
    /// </summary>
    public static (ReadOnlyMemory<byte> Payload, TimeSpan? Sliding) Decode(byte[] encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        if (encoded.Length == 0)
        {
            throw new InvalidOperationException("The Redis cache value is empty and was not written in this provider's format.");
        }

        // 头部字节决定布局；滑动过期的值至少要容得下 8 字节窗口，否则按损坏处理而不是静默当作绝对过期
        return encoded[0] switch
        {
            Absolute => (new ReadOnlyMemory<byte>(encoded, 1, encoded.Length - 1), null),
            Sliding when encoded.Length >= 1 + sizeof(long) => (
                new ReadOnlyMemory<byte>(encoded, 1 + sizeof(long), encoded.Length - 1 - sizeof(long)),
                TimeSpan.FromMilliseconds(BinaryPrimitives.ReadInt64LittleEndian(encoded.AsSpan(1)))),
            _ => throw new InvalidOperationException("The Redis cache value has an invalid header and was not written in this provider's format."),
        };
    }
}
