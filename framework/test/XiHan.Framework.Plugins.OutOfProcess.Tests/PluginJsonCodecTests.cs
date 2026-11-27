// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
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

    [Theory]
    [InlineData("k\\ud800")]
    [InlineData("\\udc00k")]
    [InlineData("k\\ude00\\ud83d")]
    public void Decode_WithAnEscapedLoneSurrogate_IsNotAMessage(string escaped)
    {
        // 中间件的 before 改写的 Baggage 由此读入（ADR-0334）：转义写出的孤立代理项同样读不出，进不了上下文
        var json = "{\"action\":\"MIDDLEWARE_ACTION_CONTINUE\",\"baggage\":{\"xihan.idempotency-key\":\"" + escaped + "\"}}";

        Assert.Null(PluginJsonCodec.Decode<MiddlewareResponse>(Encoding.UTF8.GetBytes(json)));
        Assert.Equal("k😀", PluginJsonCodec.Decode<MiddlewareResponse>("{\"baggage\":{\"xihan.idempotency-key\":\"k\\ud83d\\ude00\"}}"u8)!.Baggage["xihan.idempotency-key"]);
    }

    [Theory]
    [InlineData("{\"operation\":\"pings.\\ud800create\"}")]
    [InlineData("{\"operation\":\"pings.create\",\"extra\":\"\\udc00\"}")]
    [InlineData("{\"operation\":\"pings.create\",\"\\ud800extra\":1}")]
    [InlineData("{\"operation\":\"pings.create\",\"extra\":[{\"a\":\"\\ude00\\ud83d\"}]}")]
    [InlineData("{\"operation\":\"pings.create\",\"metadata\":{\"baggage\":{\"xihan.\\ud800\":\"v\"}}}")]
    public void Decode_WithAnEscapedLoneSurrogateAnywhere_IsNotAMessage(string json)
    {
        // RFC-0001「JSON 文本」的共同检查（wasm.md §2，ADR-0345）：已知字段、未知字段的值与成员名、映射的键都算
        Assert.Null(PluginJsonCodec.Decode<InvokeRequest>(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData("{\"operation\":\"pings.create\",\"operation\":\"wat.call\"}")]
    [InlineData("{\"operation\":\"pings.create\",\"extra\":1,\"extra\":2}")]
    [InlineData("{\"operation\":\"pings.create\",\"metadata\":{\"baggage\":{\"xihan.tenant\":\"a\",\"xihan.tenant\":\"b\"}}}")]
    [InlineData("{\"operation\":\"pings.create\",\"extra\":{\"a\":1,\"\\u0061\":2}}")]
    public void Decode_WithADuplicateMember_IsNotAMessage(string json)
    {
        // 修之前 proto3 JSON 的解析器后一个覆盖前一个（映射同此），未知字段里的不看：同一条消息两个读的一方可能读出不同的字段
        Assert.Null(PluginJsonCodec.Decode<InvokeRequest>(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void Decode_NestedDeeperThan64Levels_IsNotAMessage()
    {
        // 最外层的对象是第 1 层：未知字段里 63 层数组到第 64 层为止读得出，64 层即第 65 层
        Assert.NotNull(PluginJsonCodec.Decode<InvokeRequest>(Encoding.UTF8.GetBytes("{\"operation\":\"pings.create\",\"extra\":" + new string('[', 63) + new string(']', 63) + "}")));
        Assert.Null(PluginJsonCodec.Decode<InvokeRequest>(Encoding.UTF8.GetBytes("{\"operation\":\"pings.create\",\"extra\":" + new string('[', 64) + new string(']', 64) + "}")));

        // 已知字段同样：内部错误一层套一层，status 是第 2 层、error 是第 3 层，61 层内部错误到第 64 层，62 层即第 65 层
        Assert.NotNull(PluginJsonCodec.Decode<InvokeResponse>(NestedErrors(61)));
        Assert.Null(PluginJsonCodec.Decode<InvokeResponse>(NestedErrors(62)));
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

    /// <summary>
    /// 一条失败的应答，错误带着 <paramref name="inners"/> 层内部错误
    /// </summary>
    private static byte[] NestedErrors(int inners)
    {
        var error = new StringBuilder("{\"code\":\"ORDERS_UNEXPECTED_001\"");
        for (var i = 0; i < inners; i++)
        {
            error.Append(",\"inner\":{\"code\":\"ORDERS_UNEXPECTED_001\"");
        }

        error.Append('}', inners + 1);
        return Encoding.UTF8.GetBytes("{\"status\":{\"ok\":false,\"error\":" + error + "}}");
    }
}
