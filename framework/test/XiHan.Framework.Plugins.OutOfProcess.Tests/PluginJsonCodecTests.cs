// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Unicode;
using XiHan.Framework.Plugins.Protocol.Mapping;
using XiHan.Framework.Plugins.Protocol.V1;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// Wasm 载体的 proto3 JSON 编码按严格的 UTF-8 在字符串与字节之间转换
/// </summary>
/// <remarks>
/// <c>spec/plugin/wasm.md</c> §2 与 RFC-0001「JSON 文本」（ADR-0312）：读客体写来的字节时，非法的 UTF-8 字节序列与不是 JSON 一样得到空；写给客体的字节总是合法的 UTF-8
/// </remarks>
public sealed class PluginJsonCodecTests
{
    [Theory]
    [InlineData(new byte[] { 0xC3 })]
    [InlineData(new byte[] { 0xFF })]
    [InlineData(new byte[] { 0xC0, 0xAF })]
    [InlineData(new byte[] { 0xED, 0xA0, 0x80 })]
    [InlineData(new byte[] { 0xF4, 0x90, 0x80, 0x80 })]
    public void Decode_WithAnInvalidUtf8Sequence_IsNotAMessage(byte[] invalid)
    {
        // 截断、不能出现的字节、过长编码、编码成 UTF-8 的代理项、超出 U+10FFFF：修之前都换成 U+FFFD 读出一条消息
        byte[] bytes = [.. "{\"operation\":\"pings."u8, .. invalid, .. "create\"}"u8];

        Assert.Null(PluginJsonCodec.Decode<InvokeRequest>(bytes));
    }

    [Fact]
    public void Decode_WithAnInvalidUtf8SequenceInAnIgnoredField_IsNotAMessage_Either()
    {
        // 未知字段照旧忽略，但整条文本先要是合法的 UTF-8
        Assert.Null(PluginJsonCodec.Decode<InvokeRequest>([.. "{\"operation\":\"pings.create\",\"extra\":\""u8, 0xC3, .. "\"}"u8]));
        Assert.Equal("pings.create", PluginJsonCodec.Decode<InvokeRequest>("{\"operation\":\"pings.create\",\"extra\":\"x\"}"u8)!.Operation);
    }

    [Fact]
    public void Decode_WithMultiByteText_ReadsItAsBefore()
    {
        Assert.Equal("订单.😀", PluginJsonCodec.Decode<InvokeRequest>("{\"operation\":\"订单.😀\"}"u8)!.Operation);
    }

    [Fact]
    public void Encode_RefusesALoneSurrogate_InsteadOfWritingU_FFFD()
    {
        // 写给客体的方向：proto3 JSON 的写出器遇到孤立的代理项即抛出，写出的字节因此总是合法的 UTF-8
        Assert.ThrowsAny<ArgumentException>(() => PluginJsonCodec.Encode(new InvokeRequest { Operation = "pings.\uD800create" }));

        var paired = PluginJsonCodec.Encode(new InvokeRequest { Operation = "订单.😀" });
        Assert.True(Utf8.IsValid(paired));
        Assert.Equal("订单.😀", PluginJsonCodec.Decode<InvokeRequest>(paired)!.Operation);
    }
}
