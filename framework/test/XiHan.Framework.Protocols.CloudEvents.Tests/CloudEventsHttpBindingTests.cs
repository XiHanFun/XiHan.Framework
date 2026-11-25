// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.AspNetCore.Http;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Protocols.CloudEvents.Inbound;

namespace XiHan.Framework.Protocols.CloudEvents.Tests;

/// <summary>
/// HTTP 绑定本身：三种入站模式的读取、两种出站模式的写出、头值编码
/// </summary>
public sealed class CloudEventsHttpBindingTests
{
    /// <summary>
    /// 缺省选项（请求体与 batch 的上限）
    /// </summary>
    private static readonly CloudEventsProtocolOptions _options = new();

    /// <summary>
    /// 一个带时间、主题、数据模式、租户扩展与 JSON 数据的信封
    /// </summary>
    private static readonly MessageEnvelope _envelope = new MessageEnvelope
    {
        Id = "evt-1",
        Source = "/orders",
        Type = "orders.placed",
        Time = new DateTimeOffset(2026, 10, 23, 8, 0, 0, TimeSpan.Zero),
        Subject = "order/中文 1",
        DataSchema = "https://example.com/schemas/订单 v1.json",
        Data = "{\"id\":1}"u8.ToArray(),
    }.WithExtension(EnvelopeExtensions.Tenant, "acme");

    /// <summary>
    /// 构造一个带头与正文的请求
    /// </summary>
    private static HttpRequest Request(string? contentType, string body, params (string Name, string Value)[] headers)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        if (contentType is not null)
        {
            context.Request.ContentType = contentType;
        }

        foreach (var (name, value) in headers)
        {
            context.Request.Headers[name] = value;
        }

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return context.Request;
    }

    [Fact]
    public async Task Read_BinaryMode_MapsHeadersAndBody()
    {
        var request = Request("application/json; charset=utf-8", """{"id":1}""",
            ("ce-specversion", "1.0"), ("ce-id", "evt-1"), ("ce-source", "/orders"), ("ce-type", "orders.placed"),
            ("ce-time", "2026-10-23T08:00:00Z"), ("ce-subject", "order/%E4%B8%AD%E6%96%87%201"), ("ce-dataschema", "https://example.com/schemas/%E8%AE%A2%E5%8D%95%20v1.json"), ("ce-xihantenant", "acme"), ("CE-Trace", "x"));

        Assert.Equal(CloudEventsContentMode.Binary, CloudEventsHttpBinding.ModeOf(request).ValueOrThrow());
        var envelope = Assert.Single((await CloudEventsHttpBinding.ReadAsync(request, _options, CancellationToken.None)).ValueOrThrow());
        Assert.Equal(("evt-1", "/orders", "orders.placed"), (envelope.Id, envelope.Source, envelope.Type));
        Assert.Equal(_envelope.Time, envelope.Time);
        Assert.Equal("order/中文 1", envelope.Subject);
        Assert.Equal("https://example.com/schemas/订单 v1.json", envelope.DataSchema);
        Assert.Equal("application/json; charset=utf-8", envelope.DataContentType);
        Assert.Equal("""{"id":1}""", Encoding.UTF8.GetString(envelope.Data.Span));
        Assert.Equal("acme", envelope.Extension(EnvelopeExtensions.Tenant).ValueOrThrow());
        Assert.Equal("x", envelope.Extension("trace").ValueOrThrow());
        Assert.False(envelope.Extension("dataschema").HasValue);

        // 缺必填属性、规范版本不对、时间不合法、扩展名不合法
        Assert.Equal(MessagingErrors.InvalidEnvelope, (await CloudEventsHttpBinding.ReadAsync(Request("application/json", "{}", ("ce-specversion", "1.0"), ("ce-id", "1")), _options, CancellationToken.None)).Error?.Code);
        Assert.Equal(MessagingErrors.InvalidEnvelope, (await CloudEventsHttpBinding.ReadAsync(Request("application/json", "{}", ("ce-specversion", "0.3"), ("ce-id", "1"), ("ce-source", "/s"), ("ce-type", "t")), _options, CancellationToken.None)).Error?.Code);
        Assert.Equal(MessagingErrors.InvalidEnvelope, (await CloudEventsHttpBinding.ReadAsync(Request("application/json", "{}", ("ce-specversion", "1.0"), ("ce-id", "1"), ("ce-source", "/s"), ("ce-type", "t"), ("ce-time", "yesterday")), _options, CancellationToken.None)).Error?.Code);
        Assert.Equal(MessagingErrors.InvalidEnvelope, (await CloudEventsHttpBinding.ReadAsync(Request("application/json", "{}", ("ce-specversion", "1.0"), ("ce-id", "1"), ("ce-source", "/s"), ("ce-type", "t"), ("ce-a-b", "x")), _options, CancellationToken.None)).Error?.Code);
    }

    [Fact]
    public async Task Read_BinaryMode_RejectsTheDataHeader_ItsNameIsReserved()
    {
        // 扩展属性名不能是保留名（RFC-0009「信封」，ADR-0298）：data 是结构化 JSON 格式的成员；以前读成同名的扩展属性
        var request = Request("application/json", "{}", ("ce-specversion", "1.0"), ("ce-id", "1"), ("ce-source", "/s"), ("ce-type", "t"), ("ce-data", "x"));

        var error = (await CloudEventsHttpBinding.ReadAsync(request, _options, CancellationToken.None)).Error;

        Assert.Equal((MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalidExtension), (error?.Code, error?.MessageKey));
    }

    [Fact]
    public async Task Read_BinaryMode_ReadsTheDataSchemaHeader_AsTheAttribute()
    {
        // ce-dataschema 是信封承载的规范属性（ADR-0305）：ADR-0298 时它被当成名字是保留名的扩展属性，整个请求 400
        var request = Request("application/json", "{}", ("ce-specversion", "1.0"), ("ce-id", "1"), ("ce-source", "/s"), ("ce-type", "t"), ("ce-dataschema", "https://example.com/schemas/order.json"));

        var envelope = Assert.Single((await CloudEventsHttpBinding.ReadAsync(request, _options, CancellationToken.None)).ValueOrThrow());

        Assert.Equal("https://example.com/schemas/order.json", envelope.DataSchema);
        Assert.Empty(envelope.Extensions);
    }

    [Fact]
    public async Task Read_BinaryMode_AnEmptyDataSchemaHeader_IsNoDataSchema_AndABlankOneIsInvalid()
    {
        // 头值为空与没有这个头相同（同 ce-subject、ce-time）；只有空白的由信封拒绝，与 ce-source 只有空白时同一条路
        var empty = Request("application/json", "{}", ("ce-specversion", "1.0"), ("ce-id", "1"), ("ce-source", "/s"), ("ce-type", "t"), ("ce-dataschema", ""));
        var blank = Request("application/json", "{}", ("ce-specversion", "1.0"), ("ce-id", "1"), ("ce-source", "/s"), ("ce-type", "t"), ("ce-dataschema", "%20"));

        var read = Assert.Single((await CloudEventsHttpBinding.ReadAsync(empty, _options, CancellationToken.None)).ValueOrThrow());
        var error = (await CloudEventsHttpBinding.ReadAsync(blank, _options, CancellationToken.None)).Error;

        Assert.Null(read.DataSchema);
        Assert.Equal((MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalid), (error?.Code, error?.MessageKey));
    }

    [Theory]
    [InlineData("2026-10-23T08:00:00")]
    [InlineData("2026-10-23 08:00:00Z")]
    [InlineData("2026-10-23")]
    [InlineData("2026-10-23T08:00:00+0800")]
    [InlineData("Fri, 23 Oct 2026 08:00:00 GMT")]
    public async Task Read_BinaryMode_RejectsATime_OutsideTheRfc3339Grammar(string time)
    {
        // ce-time 与结构化 JSON 的 time 是同一条文法（RFC-0009「信封」第 3 条，ADR-0283）：通用的日期解析照收这些写法，没有偏移的按本机时区读
        var request = Request("application/json", "{}", ("ce-specversion", "1.0"), ("ce-id", "1"), ("ce-source", "/s"), ("ce-type", "t"), ("ce-time", time));

        var error = (await CloudEventsHttpBinding.ReadAsync(request, _options, CancellationToken.None)).Error;

        Assert.Equal((MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalidTime), (error?.Code, error?.MessageKey));
    }

    [Fact]
    public async Task Read_BinaryMode_ReadsTheTime_AsAUtcInstant()
    {
        var request = Request("application/json", "{}", ("ce-specversion", "1.0"), ("ce-id", "1"), ("ce-source", "/s"), ("ce-type", "t"), ("ce-time", "2026-10-23T16:00:00.5+08:00"));

        var envelope = Assert.Single((await CloudEventsHttpBinding.ReadAsync(request, _options, CancellationToken.None)).ValueOrThrow());

        Assert.Equal(new DateTimeOffset(2026, 10, 23, 8, 0, 0, 500, TimeSpan.Zero), envelope.Time);
        Assert.Equal(TimeSpan.Zero, envelope.Time!.Value.Offset);
    }

    [Fact]
    public async Task Read_StructuredAndBatchModes_UseTheStructuredFormat()
    {
        var structured = Encoding.UTF8.GetString(CloudEventsFormat.Encode(_envelope));
        var one = Assert.Single((await CloudEventsHttpBinding.ReadAsync(Request("application/cloudevents+json; charset=utf-8", structured), _options, CancellationToken.None)).ValueOrThrow());
        EnvelopeAssert.Same(_envelope, one);

        var batch = (await CloudEventsHttpBinding.ReadAsync(Request(CloudEventsHttpBinding.BatchContentType, $"[{structured},{structured.Replace("evt-1", "evt-2", StringComparison.Ordinal)}]"), _options, CancellationToken.None)).ValueOrThrow();
        Assert.Equal(["evt-1", "evt-2"], batch.Select(static e => e.Id));

        Assert.Equal(MessagingErrors.InvalidEnvelope, (await CloudEventsHttpBinding.ReadAsync(Request(CloudEventsHttpBinding.BatchContentType, structured), _options, CancellationToken.None)).Error?.Code);
        Assert.Equal(MessagingErrors.InvalidEnvelope, (await CloudEventsHttpBinding.ReadAsync(Request(CloudEventsHttpBinding.BatchContentType, "[{}]"), _options, CancellationToken.None)).Error?.Code);
        Assert.Equal(MessagingErrors.InvalidEnvelope, (await CloudEventsHttpBinding.ReadAsync(Request(CloudEventsHttpBinding.StructuredContentType, "nope"), _options, CancellationToken.None)).Error?.Code);

        // 既不是结构化内容类型也没有 ce-specversion
        var plain = await CloudEventsHttpBinding.ReadAsync(Request("application/json", "{}"), _options, CancellationToken.None);
        Assert.Equal(CloudEventsErrors.UnsupportedContentType, plain.Error?.Code);
        Assert.False(CloudEventsHttpBinding.ModeOf(Request(null, "{}")).HasValue);
    }

    [Fact]
    public async Task Write_BinaryAndStructured_AndHeaderEncoding()
    {
        using var binary = new HttpRequestMessage(HttpMethod.Post, "https://hooks.test/orders");
        CloudEventsHttpBinding.Write(_envelope, binary, CloudEventsContentMode.Binary);
        Assert.Equal("1.0", binary.Headers.GetValues("ce-specversion").Single());
        Assert.Equal("evt-1", binary.Headers.GetValues("ce-id").Single());
        Assert.Equal("/orders", binary.Headers.GetValues("ce-source").Single());
        Assert.Equal("orders.placed", binary.Headers.GetValues("ce-type").Single());
        Assert.Equal("2026-10-23T08:00:00.0000000+00:00", binary.Headers.GetValues("ce-time").Single());
        Assert.Equal("order/%E4%B8%AD%E6%96%87 1", binary.Headers.GetValues("ce-subject").Single());
        Assert.Equal("https://example.com/schemas/%E8%AE%A2%E5%8D%95 v1.json", binary.Headers.GetValues("ce-dataschema").Single());
        Assert.Equal("acme", binary.Headers.GetValues("ce-xihantenant").Single());
        Assert.Equal("application/json", binary.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("""{"id":1}""", await binary.Content.ReadAsStringAsync());

        using var structured = new HttpRequestMessage(HttpMethod.Post, "https://hooks.test/orders");
        CloudEventsHttpBinding.Write(_envelope, structured, CloudEventsContentMode.Structured);
        Assert.Equal(CloudEventsHttpBinding.StructuredContentType, structured.Content!.Headers.ContentType!.MediaType);
        EnvelopeAssert.Same(_envelope, CloudEventsFormat.Decode(await structured.Content.ReadAsByteArrayAsync()).ValueOrThrow());

        Assert.Throws<ArgumentException>(() => CloudEventsHttpBinding.Write(_envelope, new HttpRequestMessage(), CloudEventsContentMode.Batch));

        // 没有数据模式就不写 ce-dataschema 头
        using var withoutSchema = new HttpRequestMessage(HttpMethod.Post, "https://hooks.test/orders");
        CloudEventsHttpBinding.Write(_envelope with { DataSchema = null }, withoutSchema, CloudEventsContentMode.Binary);
        Assert.False(withoutSchema.Headers.Contains("ce-dataschema"));

        // 可打印 ASCII 原样（含空格），% 与非 ASCII、控制字符按 UTF-8 百分号编码
        Assert.Equal("a%25b x%C2%A0%C3%A9%0A", CloudEventsHttpBinding.EncodeHeaderValue("a%b x é\n"));
        Assert.Equal("a%b x é\n", CloudEventsHttpBinding.DecodeHeaderValue("a%25b x%C2%A0%C3%A9%0A"));
    }
}
