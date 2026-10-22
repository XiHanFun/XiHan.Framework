// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Unicode;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Grpc;

/// <summary>
/// 按消息描述把 protobuf 线格式解码为 JSON 对象。描述里没有的字段号跳过；重复的单值字段取最后一个；repeated 的数值接受打包与不打包两种形式；
/// 线类型与描述不符、截断、非法 UTF-8 等抛出 <see cref="ProtoException"/>。线上没有的字段不出现在 JSON 里（proto3 默认值由消费方补）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class ProtoDecoder
{
    /// <summary>
    /// varint 线类型。
    /// </summary>
    private const int WireVarint = 0;

    /// <summary>
    /// fixed64 线类型。
    /// </summary>
    private const int WireFixed64 = 1;

    /// <summary>
    /// 长度前缀线类型。
    /// </summary>
    private const int WireLengthDelimited = 2;

    /// <summary>
    /// fixed32 线类型。
    /// </summary>
    private const int WireFixed32 = 5;

    /// <summary>
    /// 解码一个消息，写成 JSON 对象。
    /// </summary>
    public static void Decode(ProtoMessage message, ReadOnlyMemory<byte> input, Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(writer);
        var values = Parse(input);
        writer.WriteStartObject();
        foreach (var field in message.Fields)
        {
            if (!values.TryGetValue(field.Number, out var raws))
            {
                continue;
            }

            var path = message.Name + "." + field.JsonName;
            writer.WritePropertyName(field.JsonName);
            switch (field.Cardinality)
            {
                case ProtoCardinality.Repeated:
                    writer.WriteStartArray();
                    foreach (var raw in raws)
                    {
                        // 可打包的标量以长度前缀出现时是打包形式，否则是单个元素
                        if (field.Value.IsPackable && raw.WireType == WireLengthDelimited)
                        {
                            WritePacked(field.Value, raw.Bytes, writer, path);
                        }
                        else
                        {
                            WriteValue(field.Value, raw, writer, path);
                        }
                    }

                    writer.WriteEndArray();
                    break;
                case ProtoCardinality.Map:
                    writer.WriteStartObject();
                    foreach (var raw in raws)
                    {
                        WriteMapEntry(field.Value, raw, writer, path);
                    }

                    writer.WriteEndObject();
                    break;
                default:
                    WriteValue(field.Value, raws[^1], writer, path);
                    break;
            }
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// 线上的一个字段值：线类型、varint / fixed 的位，或长度前缀的内容。
    /// </summary>
    /// <param name="WireType">线类型。</param>
    /// <param name="Bits">varint 或 fixed 的位。</param>
    /// <param name="Bytes">长度前缀的内容。</param>
    private readonly record struct RawValue(int WireType, ulong Bits, ReadOnlyMemory<byte> Bytes);

    /// <summary>
    /// 把一段消息拆成字段号 → 值列表（保持出现顺序）；分组（已废弃的线类型 3 / 4）不支持。
    /// </summary>
    private static Dictionary<int, List<RawValue>> Parse(ReadOnlyMemory<byte> input)
    {
        var values = new Dictionary<int, List<RawValue>>();
        var position = 0;
        while (position < input.Length)
        {
            var tag = ReadVarint(input.Span, ref position);
            var number = (int)(tag >> 3);
            var wireType = (int)(tag & 7);
            if (number <= 0)
            {
                throw new ProtoException("A field number of zero is not valid.");
            }

            RawValue raw;
            switch (wireType)
            {
                case WireVarint:
                    raw = new RawValue(wireType, ReadVarint(input.Span, ref position), default);
                    break;
                case WireFixed64:
                    raw = new RawValue(wireType, ReadFixed(input.Span, ref position, sizeof(ulong)), default);
                    break;
                case WireFixed32:
                    raw = new RawValue(wireType, ReadFixed(input.Span, ref position, sizeof(uint)), default);
                    break;
                case WireLengthDelimited:
                    var length = ReadVarint(input.Span, ref position);
                    if (length > (ulong)(input.Length - position))
                    {
                        throw new ProtoException("A length-delimited field exceeds the message.");
                    }

                    raw = new RawValue(wireType, 0, input.Slice(position, (int)length));
                    position += (int)length;
                    break;
                default:
                    throw new ProtoException($"Wire type {wireType} is not supported.");
            }

            if (!values.TryGetValue(number, out var list))
            {
                list = [];
                values[number] = list;
            }

            list.Add(raw);
        }

        return values;
    }

    /// <summary>
    /// 写一个单值。
    /// </summary>
    private static void WriteValue(ProtoValueType type, RawValue raw, Utf8JsonWriter writer, string path)
    {
        switch (type.Kind)
        {
            case ProtoKind.Bool:
                Expect(raw, WireVarint, path);
                writer.WriteBooleanValue(raw.Bits != 0);
                break;
            case ProtoKind.Varint32:
                Expect(raw, WireVarint, path);
                writer.WriteNumberValue(unchecked((int)(long)raw.Bits));
                break;
            case ProtoKind.Varint64:
                Expect(raw, WireVarint, path);
                writer.WriteNumberValue(unchecked((long)raw.Bits));
                break;
            case ProtoKind.Fixed32:
                Expect(raw, WireFixed32, path);
                writer.WriteNumberValue(BitConverter.UInt32BitsToSingle((uint)raw.Bits));
                break;
            case ProtoKind.Fixed64:
                Expect(raw, WireFixed64, path);
                writer.WriteNumberValue(BitConverter.UInt64BitsToDouble(raw.Bits));
                break;
            case ProtoKind.Text:
                Expect(raw, WireLengthDelimited, path);
                writer.WriteStringValue(ValidUtf8(raw.Bytes.Span, path));
                break;
            case ProtoKind.Bytes:
                Expect(raw, WireLengthDelimited, path);
                writer.WriteBase64StringValue(raw.Bytes.Span);
                break;
            case ProtoKind.TextNumber:
                Expect(raw, WireLengthDelimited, path);
                writer.WriteRawValue(ValidJson(Encoding.UTF8.GetString(ValidUtf8(raw.Bytes.Span, path)), JsonValueKind.Number, path));
                break;
            case ProtoKind.Json:
                Expect(raw, WireLengthDelimited, path);
                writer.WriteRawValue(ValidJson(Encoding.UTF8.GetString(ValidUtf8(raw.Bytes.Span, path)), null, path));
                break;
            case ProtoKind.Message:
                Expect(raw, WireLengthDelimited, path);
                Decode(type.Message!, raw.Bytes, writer);
                break;
            default:
                throw new InvalidOperationException($"Unknown proto kind '{type.Kind}'.");
        }
    }

    /// <summary>
    /// 写打包的标量序列：varint 连读，或按 4 / 8 字节连读。
    /// </summary>
    private static void WritePacked(ProtoValueType type, ReadOnlyMemory<byte> payload, Utf8JsonWriter writer, string path)
    {
        var position = 0;
        while (position < payload.Length)
        {
            var raw = type.Kind switch
            {
                ProtoKind.Fixed32 => new RawValue(WireFixed32, ReadFixed(payload.Span, ref position, sizeof(uint)), default),
                ProtoKind.Fixed64 => new RawValue(WireFixed64, ReadFixed(payload.Span, ref position, sizeof(ulong)), default),
                _ => new RawValue(WireVarint, ReadVarint(payload.Span, ref position), default),
            };
            WriteValue(type, raw, writer, path);
        }
    }

    /// <summary>
    /// 写一个映射条目：条目消息里 1 是键、2 是值；缺键按空串，缺值按类型默认值。
    /// </summary>
    private static void WriteMapEntry(ProtoValueType valueType, RawValue raw, Utf8JsonWriter writer, string path)
    {
        Expect(raw, WireLengthDelimited, path);
        var entry = Parse(raw.Bytes);
        var key = entry.TryGetValue(1, out var keys) ? keys[^1] : new RawValue(WireLengthDelimited, 0, ReadOnlyMemory<byte>.Empty);
        Expect(key, WireLengthDelimited, path + ".key");
        writer.WritePropertyName(ValidUtf8(key.Bytes.Span, path + ".key"));
        if (entry.TryGetValue(2, out var values))
        {
            WriteValue(valueType, values[^1], writer, path + ".value");
        }
        else
        {
            WriteDefault(valueType, writer);
        }
    }

    /// <summary>
    /// 类型的 proto3 默认值。
    /// </summary>
    private static void WriteDefault(ProtoValueType type, Utf8JsonWriter writer)
    {
        switch (type.Kind)
        {
            case ProtoKind.Bool:
                writer.WriteBooleanValue(false);
                break;
            case ProtoKind.Varint32:
            case ProtoKind.Varint64:
            case ProtoKind.Fixed32:
            case ProtoKind.Fixed64:
            case ProtoKind.TextNumber:
                writer.WriteNumberValue(0);
                break;
            case ProtoKind.Text:
            case ProtoKind.Bytes:
                writer.WriteStringValue(string.Empty);
                break;
            case ProtoKind.Json:
                writer.WriteNullValue();
                break;
            default:
                writer.WriteStartObject();
                writer.WriteEndObject();
                break;
        }
    }

    /// <summary>
    /// 线类型必须与描述一致。
    /// </summary>
    private static void Expect(RawValue raw, int wireType, string path)
    {
        if (raw.WireType != wireType)
        {
            throw new ProtoException($"'{path}' has wire type {raw.WireType} but the schema expects {wireType}.");
        }
    }

    /// <summary>
    /// 校验 UTF-8。
    /// </summary>
    private static ReadOnlySpan<byte> ValidUtf8(ReadOnlySpan<byte> bytes, string path)
    {
        if (!Utf8.IsValid(bytes))
        {
            throw new ProtoException($"'{path}' is not valid UTF-8.");
        }

        return bytes;
    }

    /// <summary>
    /// 文本必须是合法 JSON（可限定种类），返回原文供原样写出。
    /// </summary>
    private static string ValidJson(string text, JsonValueKind? kind, string path)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (kind is { } expected && document.RootElement.ValueKind != expected)
            {
                throw new ProtoException($"'{path}' must be a JSON {expected} but is {document.RootElement.ValueKind}.");
            }
        }
        catch (JsonException exception)
        {
            throw new ProtoException($"'{path}' is not valid JSON.", exception);
        }

        return text;
    }

    /// <summary>
    /// 读 varint（最多 10 字节）。
    /// </summary>
    private static ulong ReadVarint(ReadOnlySpan<byte> input, ref int position)
    {
        ulong result = 0;
        for (var shift = 0; shift < 64; shift += 7)
        {
            if (position >= input.Length)
            {
                throw new ProtoException("The message ends inside a varint.");
            }

            var current = input[position++];
            result |= (ulong)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
            {
                return result;
            }
        }

        throw new ProtoException("A varint is longer than 10 bytes.");
    }

    /// <summary>
    /// 读小端定长整数（4 或 8 字节）。
    /// </summary>
    private static ulong ReadFixed(ReadOnlySpan<byte> input, ref int position, int size)
    {
        if (position + size > input.Length)
        {
            throw new ProtoException("The message ends inside a fixed-size field.");
        }

        var value = size == sizeof(ulong)
            ? BinaryPrimitives.ReadUInt64LittleEndian(input.Slice(position, size))
            : BinaryPrimitives.ReadUInt32LittleEndian(input.Slice(position, size));
        position += size;
        return value;
    }
}
