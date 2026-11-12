// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Text.Json;
using XiHan.Framework.Protocols.Grpc.Proto;

namespace XiHan.Framework.Protocols.Grpc.Tests.Proto;

/// <summary>
/// 解码嵌套消息的深度上限（ADR-0189）
/// </summary>
/// <remarks>
/// 递归的消息描述（树）配上调用方构造的深层嵌套：超过上限是普通的解码错误（gRPC 的 400 类），不是写 JSON 时抛出的非预期异常
/// </remarks>
public sealed class ProtoNestingTests
{
    /// <summary>
    /// 自引用的消息：每个节点可以有一个子节点
    /// </summary>
    private const string TreeSchema = """
        {"$schema":"https://json-schema.org/draft/2020-12/schema","title":"Tree","type":"object","properties":{"child":{"$ref":"#/$defs/Node"}},
          "$defs":{"Node":{"type":"object","properties":{"child":{"$ref":"#/$defs/Node"}}}}}
        """;

    [Fact]
    public void NestingBeyondTheLimit_IsADecodingError()
    {
        var message = ProtoSchema.FromJsonSchema(TreeSchema);

        // 修之前写到第 1000 层时 Utf8JsonWriter 抛出 InvalidOperationException，调用方得到 500
        var exception = Assert.Throws<ProtoException>(() => Decode(message, Nested(2000)));

        Assert.Contains(ProtoDecoder.MaxDepth.ToString(System.Globalization.CultureInfo.InvariantCulture), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NestingWithinTheLimit_Decodes()
    {
        var message = ProtoSchema.FromJsonSchema(TreeSchema);

        using var document = JsonDocument.Parse(Decode(message, Nested(ProtoDecoder.MaxDepth - 1)));

        var depth = 0;
        for (var node = document.RootElement; node.TryGetProperty("child", out var child); node = child)
        {
            depth++;
        }

        Assert.Equal(ProtoDecoder.MaxDepth - 1, depth);
    }

    /// <summary>
    /// <paramref name="levels"/> 层嵌套的 <c>child</c>（字段 1，长度前缀）
    /// </summary>
    private static byte[] Nested(int levels)
    {
        var inner = Array.Empty<byte>();
        for (var level = 0; level < levels; level++)
        {
            var length = new List<byte>();
            for (var value = (uint)inner.Length; ; value >>= 7)
            {
                if (value < 0x80)
                {
                    length.Add((byte)value);
                    break;
                }

                length.Add((byte)((value & 0x7F) | 0x80));
            }

            inner = [0x0A, .. length, .. inner];
        }

        return inner;
    }

    /// <summary>
    /// 解码为 JSON 文本
    /// </summary>
    private static string Decode(ProtoMessage message, byte[] bytes)
    {
        var output = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(output))
        {
            ProtoDecoder.Decode(message, bytes, writer);
        }

        return System.Text.Encoding.UTF8.GetString(output.WrittenSpan);
    }
}
