// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Grpc.Proto;

/// <summary>
/// 按消息描述把 JSON 对象编码为 protobuf 线格式。
/// </summary>
/// <remarks>
/// JSON 里的 <c>null</c> 与描述里没有的属性不上线；非空值都写出（含 proto3 默认值）；repeated 的数值与布尔打包写出；映射写成 <c>{1: key, 2: value}</c> 的条目。值与 Schema 不符时抛出 <see cref="ProtoException"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class ProtoEncoder
{
    /// <summary>
    /// varint 线类型。
    /// </summary>
    private const uint WireVarint = 0;

    /// <summary>
    /// fixed64 线类型。
    /// </summary>
    private const uint WireFixed64 = 1;

    /// <summary>
    /// 长度前缀线类型。
    /// </summary>
    private const uint WireLengthDelimited = 2;

    /// <summary>
    /// fixed32 线类型。
    /// </summary>
    private const uint WireFixed32 = 5;

    /// <summary>
    /// 编码一个消息。
    /// </summary>
    public static void Encode(ProtoMessage message, JsonElement value, IBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(output);
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new ProtoException($"Message '{message.Name}' requires a JSON object but the value is {value.ValueKind}.");
        }

        foreach (var property in value.EnumerateObject())
        {
            // 描述里没有的属性与 null 都不上线：protobuf 没有"显式 null"，缺席就是 null
            if (!message.TryGetField(property.Name, out var field) || property.Value.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            WriteField(field, property.Value, output, message.Name + "." + property.Name);
        }
    }

    /// <summary>
    /// 编码一个字段：按基数分派。
    /// </summary>
    private static void WriteField(ProtoField field, JsonElement value, IBufferWriter<byte> output, string path)
    {
        switch (field.Cardinality)
        {
            case ProtoCardinality.Repeated:
                if (value.ValueKind != JsonValueKind.Array)
                {
                    throw new ProtoException($"'{path}' must be a JSON array.");
                }

                if (field.Value.IsPackable)
                {
                    // 打包：所有元素的载荷连在一起，作为一个长度前缀字段写出
                    var packed = new ArrayBufferWriter<byte>();
                    foreach (var item in value.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Null)
                        {
                            WriteScalarPayload(field.Value, item, packed, path);
                        }
                    }

                    if (packed.WrittenCount > 0)
                    {
                        WriteTag(output, field.Number, WireLengthDelimited);
                        WriteLengthDelimited(output, packed.WrittenSpan);
                    }
                }
                else
                {
                    foreach (var item in value.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Null)
                        {
                            WriteValue(field.Number, field.Value, item, output, path);
                        }
                    }
                }

                break;
            case ProtoCardinality.Map:
                if (value.ValueKind != JsonValueKind.Object)
                {
                    throw new ProtoException($"'{path}' must be a JSON object.");
                }

                foreach (var entry in value.EnumerateObject())
                {
                    // 每个条目是一条嵌套消息：1 = 键（字符串），2 = 值；null 值的条目只带键
                    var buffer = new ArrayBufferWriter<byte>();
                    WriteTag(buffer, 1, WireLengthDelimited);
                    WriteLengthDelimited(buffer, Encoding.UTF8.GetBytes(entry.Name));
                    if (entry.Value.ValueKind != JsonValueKind.Null)
                    {
                        WriteValue(2, field.Value, entry.Value, buffer, path + "." + entry.Name);
                    }

                    WriteTag(output, field.Number, WireLengthDelimited);
                    WriteLengthDelimited(output, buffer.WrittenSpan);
                }

                break;
            default:
                WriteValue(field.Number, field.Value, value, output, path);
                break;
        }
    }

    /// <summary>
    /// 写一个带标签的值。
    /// </summary>
    private static void WriteValue(int number, ProtoValueType type, JsonElement value, IBufferWriter<byte> output, string path)
    {
        switch (type.Kind)
        {
            case ProtoKind.Bool:
            case ProtoKind.Varint32:
            case ProtoKind.Varint64:
                WriteTag(output, number, WireVarint);
                WriteScalarPayload(type, value, output, path);
                break;
            case ProtoKind.Fixed64:
                WriteTag(output, number, WireFixed64);
                WriteScalarPayload(type, value, output, path);
                break;
            case ProtoKind.Fixed32:
                WriteTag(output, number, WireFixed32);
                WriteScalarPayload(type, value, output, path);
                break;
            case ProtoKind.Text:
                if (value.ValueKind != JsonValueKind.String)
                {
                    throw new ProtoException($"'{path}' must be a JSON string.");
                }

                WriteTag(output, number, WireLengthDelimited);
                WriteLengthDelimited(output, Encoding.UTF8.GetBytes(value.GetString()!));
                break;
            case ProtoKind.Bytes:
                if (value.ValueKind != JsonValueKind.String || !value.TryGetBytesFromBase64(out var bytes))
                {
                    throw new ProtoException($"'{path}' must be a base64 string.");
                }

                WriteTag(output, number, WireLengthDelimited);
                WriteLengthDelimited(output, bytes);
                break;
            case ProtoKind.TextNumber:
                // 十进制文本：JSON 数字原样上线；允许字符串形式的数字（序列化上下文可能把 decimal 写成字符串）
                if (value.ValueKind is not (JsonValueKind.Number or JsonValueKind.String))
                {
                    throw new ProtoException($"'{path}' must be a JSON number.");
                }

                WriteTag(output, number, WireLengthDelimited);
                WriteLengthDelimited(output, Encoding.UTF8.GetBytes(value.ValueKind == JsonValueKind.Number ? value.GetRawText() : value.GetString()!));
                break;
            case ProtoKind.Json:
                WriteTag(output, number, WireLengthDelimited);
                WriteLengthDelimited(output, Encoding.UTF8.GetBytes(value.GetRawText()));
                break;
            case ProtoKind.Message:
                var nested = new ArrayBufferWriter<byte>();
                Encode(type.Message!, value, nested);
                WriteTag(output, number, WireLengthDelimited);
                WriteLengthDelimited(output, nested.WrittenSpan);
                break;
            default:
                throw new InvalidOperationException($"Unknown proto kind '{type.Kind}'.");
        }
    }

    /// <summary>
    /// 写可打包标量的载荷（不带标签）：varint、fixed32 或 fixed64。
    /// </summary>
    private static void WriteScalarPayload(ProtoValueType type, JsonElement value, IBufferWriter<byte> output, string path)
    {
        switch (type.Kind)
        {
            case ProtoKind.Bool:
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    throw new ProtoException($"'{path}' must be a JSON boolean.");
                }

                WriteVarint(output, value.ValueKind == JsonValueKind.True ? 1UL : 0UL);
                break;
            case ProtoKind.Varint32:
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var int32))
                {
                    throw new ProtoException($"'{path}' must be a 32-bit integer.");
                }

                // 负数按 int64 的补码写成 10 字节 varint，与 protobuf 的 int32 一致
                WriteVarint(output, unchecked((ulong)(long)int32));
                break;
            case ProtoKind.Varint64:
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var int64))
                {
                    throw new ProtoException($"'{path}' must be a 64-bit integer.");
                }

                WriteVarint(output, unchecked((ulong)int64));
                break;
            case ProtoKind.Fixed32:
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetSingle(out var single))
                {
                    throw new ProtoException($"'{path}' must be a number.");
                }

                BinaryPrimitives.WriteUInt32LittleEndian(output.GetSpan(sizeof(uint)), BitConverter.SingleToUInt32Bits(single));
                output.Advance(sizeof(uint));
                break;
            case ProtoKind.Fixed64:
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var @double))
                {
                    throw new ProtoException($"'{path}' must be a number.");
                }

                BinaryPrimitives.WriteUInt64LittleEndian(output.GetSpan(sizeof(ulong)), BitConverter.DoubleToUInt64Bits(@double));
                output.Advance(sizeof(ulong));
                break;
            default:
                throw new InvalidOperationException($"Proto kind '{type.Kind}' has no scalar payload.");
        }
    }

    /// <summary>
    /// 写字段标签：<c>(字段号 &lt;&lt; 3) | 线类型</c> 的 varint。
    /// </summary>
    private static void WriteTag(IBufferWriter<byte> output, int number, uint wireType) => WriteVarint(output, ((ulong)number << 3) | wireType);

    /// <summary>
    /// 写长度前缀与内容。
    /// </summary>
    private static void WriteLengthDelimited(IBufferWriter<byte> output, ReadOnlySpan<byte> data)
    {
        WriteVarint(output, (ulong)data.Length);
        data.CopyTo(output.GetSpan(data.Length));
        output.Advance(data.Length);
    }

    /// <summary>
    /// 写 varint（最多 10 字节）。
    /// </summary>
    private static void WriteVarint(IBufferWriter<byte> output, ulong value)
    {
        var span = output.GetSpan(10);
        var written = 0;
        while (value >= 0x80)
        {
            span[written++] = (byte)(value | 0x80);
            value >>= 7;
        }

        span[written++] = (byte)value;
        output.Advance(written);
    }
}
