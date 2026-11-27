// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Http;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Protocols.CloudEvents.Inbound;

namespace XiHan.Framework.Protocols.CloudEvents.Tests;

/// <summary>
/// binary 模式的头值按 CloudEvents 的百分号解码严格读
/// </summary>
/// <remarks>
/// <c>spec/protocol/cloudevents.md</c>「binary 的头值」（ADR-0323）：以前 <c>Uri.UnescapeDataString</c> 把不合法的 UTF-8 百分号序列与不完整的 <c>%</c> 原样留成百分号文本，照常读出
/// </remarks>
public sealed class CloudEventsBinaryHeaderTests
{
    /// <summary>
    /// 孤立的高代理项
    /// </summary>
    private const char HighSurrogate = (char)0xD800;

    /// <summary>
    /// 缺省选项
    /// </summary>
    private static readonly CloudEventsProtocolOptions _options = new();

    /// <summary>
    /// 读不出的头值
    /// </summary>
    /// <remarks>
    /// 编码成 UTF-8 的代理项、截断的多字节字符、过长编码、超出 U+10FFFF 的码位、不可能出现在 UTF-8 里的字节、不完整或不是十六进制的 <c>%</c>
    /// </remarks>
    public static TheoryData<string> UndecodableValues =>
    [
        "%ED%A0%80",
        "a%C3",
        "%C0%A0",
        "%F4%90%80%80",
        "%FF",
        "%",
        "%4",
        "50%",
        "%zz",
        "%G0",
    ];

    /// <summary>
    /// 构造一个必填属性齐全、再加给定头的 binary 请求
    /// </summary>
    private static HttpRequest Request(params (string Name, string Value)[] headers)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.ContentType = "application/json";
        context.Request.Headers["ce-specversion"] = "1.0";
        context.Request.Headers["ce-id"] = "evt-1";
        context.Request.Headers["ce-source"] = "/orders";
        context.Request.Headers["ce-type"] = "orders.placed";
        foreach (var (name, value) in headers)
        {
            context.Request.Headers[name] = value;
        }

        context.Request.Body = new MemoryStream("{}"u8.ToArray());
        return context.Request;
    }

    [Theory]
    [MemberData(nameof(UndecodableValues))]
    public void DecodeHeaderValue_HasNoValue_ForAnUndecodableValue(string value)
    {
        Assert.False(CloudEventsHttpBinding.DecodeHeaderValue(value).HasValue);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("plain", "plain")]
    [InlineData("%41", "A")]
    [InlineData("%c3%a9", "é")]
    [InlineData("a%25b x%C2%A0%C3%A9%0A", "a%b x\u00A0é\n")]
    [InlineData("%F0%9F%98%80 😀 é", "😀 😀 é")]
    public void DecodeHeaderValue_DecodesPercentSequences_AndKeepsOtherCharacters(string value, string expected)
    {
        Assert.Equal(expected, CloudEventsHttpBinding.DecodeHeaderValue(value).ValueOrThrow());
    }

    [Theory]
    [InlineData("\"hello world\"", "hello world")]
    [InlineData("\"\"", "")]
    [InlineData("\"a\\\"b\"", "a\"b")]
    [InlineData("\"a\\\\b\"", "a\\b")]
    [InlineData("\"a\\ b\"", "a b")]
    [InlineData("a\"b c\"d", "ab cd")]
    [InlineData("\"x\" \"y\"", "x y")]
    [InlineData("a\\b", "a\\b")]
    [InlineData("\"%41\"", "A")]
    [InlineData("\"\\%41\"", "A")]
    [InlineData("\"%2541\"", "%41")]
    [InlineData("\"tab\there\"", "tab\there")]
    [InlineData("\"é 😀\"", "é 😀")]
    [InlineData("\"%E2%82%AC\"", "€")]
    public void DecodeHeaderValue_FirstUnescapesQuotedStrings_ThenDecodesOneRoundOfPercentSequences(string value, string expected)
    {
        // CloudEvents HTTP 绑定 1.0.2 §3.1.3.2：先按 RFC 7230 §3.2.6 解开双引号括起的部分（quoted-pair 是后面那个字符本身），再做一轮百分号解码（ADR-0332）；
        // 较早版本的发送方不编码空格与双引号，可能用引号括起整个值。修之前引号读成值的一部分
        Assert.Equal(expected, CloudEventsHttpBinding.DecodeHeaderValue(value).ValueOrThrow());
    }

    [Theory]
    [InlineData("a\"b")]
    [InlineData("\"abc")]
    [InlineData("\"a\\\"")]
    [InlineData("\"a\\")]
    [InlineData("\"a\u0001b\"")]
    [InlineData("\"a\nb\"")]
    [InlineData("\"a\u007Fb\"")]
    [InlineData("\"a\\\u0001\"")]
    [InlineData("\"%C3\"")]
    public void DecodeHeaderValue_HasNoValue_ForABrokenQuotedString(string value)
    {
        // 引号不配对、quoted-string 里有 qdtext 不允许的控制字符（水平制表符除外）、反斜杠在末尾或后面是控制字符，都是读不出的头（ADR-0332）；
        // 解开之后的百分号序列照旧严格解码
        Assert.False(CloudEventsHttpBinding.DecodeHeaderValue(value).HasValue);
    }

    [Fact]
    public async Task Read_BinaryMode_UnescapesAQuotedHeaderValue_AndABrokenOneIsAnInvalidEnvelope()
    {
        var envelope = Assert.Single((await CloudEventsHttpBinding.ReadAsync(Request(("ce-subject", "\"order 42\""), ("ce-note", "\"a \\\"b\\\"\"")), _options, CancellationToken.None)).ValueOrThrow());
        var broken = (await CloudEventsHttpBinding.ReadAsync(Request(("ce-subject", "\"order 42")), _options, CancellationToken.None)).Error;

        Assert.Equal("order 42", envelope.Subject);
        Assert.Equal("a \"b\"", envelope.Extension("note").ValueOrThrow());
        Assert.Equal((MessagingErrors.InvalidEnvelope, CloudEventsMessages.HeaderInvalid, "ce-subject"), (broken?.Code, broken?.MessageKey, broken?.Details!["header"]));
    }

    [Fact]
    public void DecodeHeaderValue_HasNoValue_ForALoneSurrogate_WithOrWithoutPercentSequences()
    {
        Assert.False(CloudEventsHttpBinding.DecodeHeaderValue("n" + HighSurrogate).HasValue);
        Assert.False(CloudEventsHttpBinding.DecodeHeaderValue("%41" + HighSurrogate).HasValue);
        // 引号括起的孤立代理项按 obs-text 解开，百分号解码那一步同样拒绝
        Assert.False(CloudEventsHttpBinding.DecodeHeaderValue("\"n" + HighSurrogate + "\"").HasValue);
    }

    [Theory]
    [MemberData(nameof(UndecodableValues))]
    public async Task Read_BinaryMode_AnUndecodableHeaderValue_IsAnInvalidEnvelope(string value)
    {
        var error = (await CloudEventsHttpBinding.ReadAsync(Request(("ce-subject", value)), _options, CancellationToken.None)).Error;

        Assert.Equal((MessagingErrors.InvalidEnvelope, CloudEventsMessages.HeaderInvalid), (error?.Code, error?.MessageKey));
        Assert.Equal("ce-subject", error!.Details!["header"]);
    }

    [Theory]
    [InlineData("ce-specversion")]
    [InlineData("ce-id")]
    [InlineData("ce-source")]
    [InlineData("ce-type")]
    [InlineData("ce-time")]
    [InlineData("ce-dataschema")]
    [InlineData("ce-datacontenttype")]
    [InlineData("CE-Note")]
    public async Task Read_BinaryMode_ChecksEveryCeHeader_AndNamesItInLowercase(string header)
    {
        // 规范属性与扩展属性的头都按同一条解码；头名照 HTTP 不分大小写，参数写小写
        var error = (await CloudEventsHttpBinding.ReadAsync(Request((header, "x%ED%A0%80")), _options, CancellationToken.None)).Error;

        Assert.Equal((MessagingErrors.InvalidEnvelope, CloudEventsMessages.HeaderInvalid), (error?.Code, error?.MessageKey));
        Assert.Equal(header.ToLowerInvariant(), error!.Details!["header"]);
    }

    [Fact]
    public async Task Read_BinaryMode_ALoneSurrogateWrittenDirectlyInAHeader_IsAnInvalidEnvelope()
    {
        // 放开了请求头编码的服务器或测试宿主才进得来：它编不成 UTF-8；以前照原样成了扩展属性的值
        var error = (await CloudEventsHttpBinding.ReadAsync(Request(("ce-note", "n" + HighSurrogate)), _options, CancellationToken.None)).Error;

        Assert.Equal((MessagingErrors.InvalidEnvelope, CloudEventsMessages.HeaderInvalid, "ce-note"), (error?.Code, error?.MessageKey, error?.Details!["header"]));
    }

    [Fact]
    public async Task Read_BinaryMode_DecodesUnneededAndLowercaseEncodings_AndCharactersWrittenDirectly()
    {
        var envelope = Assert.Single((await CloudEventsHttpBinding.ReadAsync(Request(("ce-subject", "%41%c3%a9 é 😀 %F0%9F%98%80"), ("ce-note", "a%25b")), _options, CancellationToken.None)).ValueOrThrow());

        Assert.Equal("Aé é 😀 😀", envelope.Subject);
        Assert.Equal("a%b", envelope.Extension("note").ValueOrThrow());
    }
}
