// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;

namespace XiHan.Framework.Protocols.Grpc.Tests.Fixtures;

/// <summary>
/// 手工拼 protobuf 线格式的小工具：测试用它构造"真实客户端会发的字节"，不经过被测的编码器。
/// </summary>
internal static class ProtoBytes
{
    /// <summary>
    /// varint。
    /// </summary>
    public static byte[] Varint(ulong value)
    {
        var bytes = new List<byte>();
        while (value >= 0x80)
        {
            bytes.Add((byte)(value | 0x80));
            value >>= 7;
        }

        bytes.Add((byte)value);
        return [.. bytes];
    }

    /// <summary>
    /// varint 字段（线类型 0）。
    /// </summary>
    public static byte[] VarintField(int number, long value) => [.. Varint((ulong)(number << 3)), .. Varint(unchecked((ulong)value))];

    /// <summary>
    /// 长度前缀字段（线类型 2）。
    /// </summary>
    public static byte[] BytesField(int number, byte[] payload) => [.. Varint((ulong)(number << 3) | 2), .. Varint((ulong)payload.Length), .. payload];

    /// <summary>
    /// 字符串字段。
    /// </summary>
    public static byte[] StringField(int number, string value) => BytesField(number, Encoding.UTF8.GetBytes(value));

    /// <summary>
    /// 拼接。
    /// </summary>
    public static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(static p => p)];
}
