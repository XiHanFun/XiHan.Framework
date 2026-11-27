// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Google.Protobuf;
using XiHan.Framework.Plugins.Protocol.Mapping;
using XiHan.Framework.Primitives;
using ProtoError = XiHan.Framework.Plugins.Protocol.V1.Error;
using ProtoStatus = XiHan.Framework.Plugins.Protocol.V1.Status;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 写进插件协议的 <c>Error</c> 文案里编不成 UTF-8 的部分换成 U+FFFD
/// </summary>
/// <remarks>
/// <c>spec/plugin/invocation.md</c> §3「写出 <c>Error</c> 的文案」，ADR-0339。两种载体共用 <see cref="PluginErrorMapper.ToProto"/>：gRPC 载体的 protobuf 编码本来就换成 U+FFFD（这里钉住），Wasm 载体的 JSON 写出器以前抛出（端到端见 Wasm 测试项目的 <c>WasmErrorTextTests</c>）。用例数据里只写占位：<c>&lt;high&gt;</c>、<c>&lt;low&gt;</c> 是孤立的高、低代理项，<c>&lt;fffd&gt;</c> 是 U+FFFD，孤立的代理项不进测试用例的名字
/// </remarks>
public sealed class PluginErrorTextTests
{
    [Theory]
    [InlineData("a<high>b", "a<fffd>b")]
    [InlineData("a<low>b", "a<fffd>b")]
    [InlineData("<low><high>", "<fffd><fffd>")]
    [InlineData("ab<high>", "ab<fffd>")]
    [InlineData("<low>ab", "<fffd>ab")]
    [InlineData("a<high><high>b", "a<fffd><fffd>b")]
    [InlineData("<high><low><low>", "<high><low><fffd>")]
    [InlineData("<high><high><low>", "<fffd><high><low>")]
    public void ToProto_TextWithLoneSurrogates_IsWrittenWithOneReplacementCharacterEach(string text, string expected)
    {
        var error = Error.Literal("ORDERS_UNEXPECTED_001", Decode(text), MessageArguments.Of(("order", "o-1")), Error.Literal("ORDERS_UNEXPECTED_002", Decode(text)));

        var proto = PluginErrorMapper.ToProto(error);

        // 只换文案；错误码、消息键、细节与内部错误的其余部分照写
        Assert.Equal(("ORDERS_UNEXPECTED_001", string.Empty, Decode(expected), """{"order":"o-1"}"""), (proto.Code, proto.MessageKey, proto.Text, proto.Details));
        Assert.Equal(("ORDERS_UNEXPECTED_002", Decode(expected)), (proto.Inner.Code, proto.Inner.Text));
        Assert.False(UnicodeText.HasLoneSurrogate(proto.Text));
    }

    [Theory]
    [InlineData("a<high>b")]
    [InlineData("<low><high>")]
    public void ToStatus_TextWithLoneSurrogates_IsWrittenByBothCarriers_AndReadBackAsReplaced(string text)
    {
        var status = PluginErrorMapper.ToStatus(Result.Failure(Error.Literal("ORDERS_UNEXPECTED_001", Decode(text))));
        var expected = Decode(text).Replace('\uD800', '�').Replace('\uDC00', '�');

        // gRPC 载体：protobuf 的字符串是合法的 UTF-8，对方按 proto3 读得进
        var binary = ProtoStatus.Parser.ParseFrom(status.ToByteArray());
        Assert.Equal(expected, binary.Error.Text);
        // Wasm 载体：修之前 JSON 写出器在这里抛出 ArgumentException
        var json = PluginJsonCodec.Decode<ProtoStatus>(PluginJsonCodec.Encode(status));
        Assert.NotNull(json);
        Assert.Equal(expected, json.Error.Text);
        // 对方读出的是一个自由文本错误，文案就是换过的
        var read = PluginErrorMapper.FromProto(json.Error, "xihan.samples.echo");
        Assert.Equal(("ORDERS_UNEXPECTED_001", expected), (read.Code, read.Text));
    }

    [Theory]
    [InlineData("plain text")]
    [InlineData("pair <high><low> and <fffd> itself")]
    [InlineData("")]
    public void ToProto_WellFormedText_IsWrittenAsIs(string text)
    {
        var proto = PluginErrorMapper.ToProto(text.Length == 0 ? new Error("ORDERS_UNEXPECTED_001", "orders.order.failed") : Error.Literal("ORDERS_UNEXPECTED_001", Decode(text)));

        Assert.Equal(Decode(text), proto.Text);
    }

    [Fact]
    public void ProtobufBinaryEncoding_ReplacesLoneSurrogates_EvenWithoutTheMapper()
    {
        // 钉住 Google.Protobuf 的二进制编码：孤立的代理项各编成 EF BF BD（U+FFFD），与 ToProto 的替换相同，不抛出
        var bytes = new ProtoError { Code = "ORDERS_UNEXPECTED_001", Text = Decode("a<high><low><low>b") }.ToByteArray();

        Assert.Equal(Decode("a<high><low><fffd>b"), ProtoError.Parser.ParseFrom(bytes).Text);
    }

    /// <summary>
    /// 把占位换成对应的码元
    /// </summary>
    private static string Decode(string text)
        => text.Replace("<high>", "\uD800", StringComparison.Ordinal).Replace("<low>", "\uDC00", StringComparison.Ordinal).Replace("<fffd>", "�", StringComparison.Ordinal);
}
