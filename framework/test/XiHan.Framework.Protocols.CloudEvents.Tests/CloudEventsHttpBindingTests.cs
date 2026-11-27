// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.AspNetCore.Http;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.CloudEvents.Inbound;

namespace XiHan.Framework.Protocols.CloudEvents.Tests;

/// <summary>
/// HTTP 绑定本身：三种入站模式的读取、两种出站模式的写出、头值编码
/// </summary>
public sealed class CloudEventsHttpBindingTests
{
    /// <summary>
    /// 孤立的高代理项
    /// </summary>
    private const char HighSurrogate = (char)0xD800;

    /// <summary>
    /// 孤立的低代理项
    /// </summary>
    private const char LowSurrogate = (char)0xDC00;

    /// <summary>
    /// 本来就是 U+FFFD 的字符
    /// </summary>
    private const char ReplacementCharacter = (char)0xFFFD;

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
    public async Task Read_BinaryMode_AnEmptyOrBlankDataSchemaHeader_IsInvalid_LikeAnEmptyStructuredMember()
    {
        // 与结构化 JSON 的 "dataschema": "" 同一个错误（RFC-0009「各传输上的 dataschema」，ADR-0323）：修之前空头与没有这个头相同，两种写法的空值读法不对称；
        // 只有空白的照旧由信封拒绝，与 ce-source 只有空白时同一条路
        var empty = Request("application/json", "{}", ("ce-specversion", "1.0"), ("ce-id", "1"), ("ce-source", "/s"), ("ce-type", "t"), ("ce-dataschema", ""));
        var blank = Request("application/json", "{}", ("ce-specversion", "1.0"), ("ce-id", "1"), ("ce-source", "/s"), ("ce-type", "t"), ("ce-dataschema", "%20"));
        var structured = Request(CloudEventsHttpBinding.StructuredContentType, """{"specversion":"1.0","id":"1","source":"/s","type":"t","dataschema":""}""");

        var expected = (await CloudEventsHttpBinding.ReadAsync(structured, _options, CancellationToken.None)).Error;
        var emptyError = (await CloudEventsHttpBinding.ReadAsync(empty, _options, CancellationToken.None)).Error;
        var blankError = (await CloudEventsHttpBinding.ReadAsync(blank, _options, CancellationToken.None)).Error;

        Assert.Equal((MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalid), (expected?.Code, expected?.MessageKey));
        Assert.Equal((expected!.Code, expected.MessageKey, expected.Details!["reason"]), (emptyError?.Code, emptyError?.MessageKey, emptyError?.Details!["reason"]));
        Assert.Equal((MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalid), (blankError?.Code, blankError?.MessageKey));
    }

    [Fact]
    public async Task Read_BinaryMode_WithoutADataSchemaHeader_HasNoDataSchema()
    {
        var envelope = Assert.Single((await CloudEventsHttpBinding.ReadAsync(Request("application/json", "{}", ("ce-specversion", "1.0"), ("ce-id", "1"), ("ce-source", "/s"), ("ce-type", "t")), _options, CancellationToken.None)).ValueOrThrow());

        Assert.Null(envelope.DataSchema);
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
        Assert.Equal("order/%E4%B8%AD%E6%96%87%201", binary.Headers.GetValues("ce-subject").Single());
        Assert.Equal("https://example.com/schemas/%E8%AE%A2%E5%8D%95%20v1.json", binary.Headers.GetValues("ce-dataschema").Single());
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

        // 空格、双引号、% 与可打印 ASCII 之外的字符（非 ASCII、控制字符）按 UTF-8 百分号编码，其余可打印 ASCII 原样（CloudEvents HTTP 绑定 1.0.2 §3.1.3.2，ADR-0332；
        // 修之前空格原样写出），解码回来是原值
        Assert.Equal("a%25b%20x%C2%A0%C3%A9%0A", CloudEventsHttpBinding.EncodeHeaderValue("a%b x é\n"));
        Assert.Equal("a%b x é\n", CloudEventsHttpBinding.DecodeHeaderValue("a%25b%20x%C2%A0%C3%A9%0A").ValueOrThrow());
    }

    [Theory]
    [InlineData("Euro € 😀", "Euro%20%E2%82%AC%20%F0%9F%98%80")]
    [InlineData("say \"hi\"", "say%20%22hi%22")]
    [InlineData(" ", "%20")]
    [InlineData("\"", "%22")]
    [InlineData("\u007F\t", "%7F%09")]
    [InlineData("!#$&'()*+,-./:;<=>?@[\\]^_`{|}~", "!#$&'()*+,-./:;<=>?@[\\]^_`{|}~")]
    public void EncodeHeaderValue_EncodesSpacesAndDoubleQuotes_AndLeavesTheOtherPrintableAscii(string value, string expected)
    {
        // CloudEvents HTTP 绑定 1.0.2 §3.1.3.2：空格（U+0020）、双引号（U+0022）、% 与 U+0021–U+007E 之外的字符必须编码，第一行是规范自己的例子（ADR-0332）；
        // 编出的值不含空格与双引号，不需要再用引号括起；修之前空格与双引号原样写出
        var encoded = CloudEventsHttpBinding.EncodeHeaderValue(value);

        Assert.Equal(expected, encoded);
        Assert.DoesNotContain(' ', encoded);
        Assert.DoesNotContain('"', encoded);
        Assert.Equal(value, CloudEventsHttpBinding.DecodeHeaderValue(encoded).ValueOrThrow());
    }

    [Fact]
    public async Task Read_BinaryMode_AnEmptySubjectOrTimeHeader_IsInvalid_LikeTheEmptyStructuredMember()
    {
        // CloudEvents 1.0 规定 subject 出现时必须非空（RFC-0009「各传输上的 dataschema」，ADR-0332）：修之前空的 ce-subject 与 ce-time 头与没有这个头相同，
        // structured 的 "subject": "" 读成空串；空的 ce-subject 与空的 ce-dataschema 同一条路，空的 ce-time 与 "time": "" 同一个错误
        (string Header, string Member, string MessageKey)[] attributes = [("ce-subject", "subject", MessagingMessages.EnvelopeInvalid), ("ce-time", "time", MessagingMessages.EnvelopeInvalidTime)];
        foreach (var (header, member, messageKey) in attributes)
        {
            var binary = Request("application/json", "{}", ("ce-specversion", "1.0"), ("ce-id", "1"), ("ce-source", "/s"), ("ce-type", "t"), (header, ""));
            var structured = Request(CloudEventsHttpBinding.StructuredContentType, $$"""{"specversion":"1.0","id":"1","source":"/s","type":"t","{{member}}":""}""");

            var expected = (await CloudEventsHttpBinding.ReadAsync(structured, _options, CancellationToken.None)).Error;
            var actual = (await CloudEventsHttpBinding.ReadAsync(binary, _options, CancellationToken.None)).Error;

            Assert.Equal((MessagingErrors.InvalidEnvelope, messageKey), (expected?.Code, expected?.MessageKey));
            Assert.Equal((expected!.Code, expected.MessageKey), (actual?.Code, actual?.MessageKey));
            Assert.Equal(expected.Details, actual!.Details);
        }
    }

    [Fact]
    public async Task Read_BinaryMode_ASubjectOfOnlySpaces_IsKept()
    {
        // 非空就合 CloudEvents 的规定：只有空白的 subject 照常读出（与 dataschema 不同，ADR-0332 只拒空串）
        var envelope = Assert.Single((await CloudEventsHttpBinding.ReadAsync(Request("application/json", "{}", ("ce-specversion", "1.0"), ("ce-id", "1"), ("ce-source", "/s"), ("ce-type", "t"), ("ce-subject", "%20")), _options, CancellationToken.None)).ValueOrThrow());

        Assert.Equal(" ", envelope.Subject);
    }

    [Fact]
    public void Envelope_RefusesAnEmptySubject_SoItIsNeverWrittenAsOne()
    {
        // 构造时就拒：写出一侧不会写出读不回来的 "subject": ""（RFC-0009「信封」的写出，ADR-0332）；修之前照收，structured 写成空串、binary 写一个空头
        var envelope = new MessageEnvelope { Id = "1", Source = "/o", Type = "t" };

        Assert.Throws<ArgumentException>(() => envelope with { Subject = string.Empty });
        Assert.Null(envelope.Subject);
        Assert.Equal(" ", (envelope with { Subject = " " }).Subject);
    }

    [Fact]
    public void Write_BinaryMode_RefusesAnAttributeWithALoneSurrogate_BeforeWritingAnyHeader()
    {
        // protocol/cloudevents.md「出站」与 RFC-0001「写出与转交」（ADR-0318）：与 structured 经 CloudEventsFormat.Encode 的拒绝相同，同一个码、同一句消息；
        // 修之前 binary 不经这道核对，头值的百分号编码把孤立的代理项编成 %EF%BF%BD，Webhook 缺省的单条发布照样写成 U+FFFD
        var envelope = new MessageEnvelope { Id = "1", Source = "/o", Type = "t" };
        (string Attribute, MessageEnvelope Envelope)[] broken =
        [
            ("id", envelope with { Id = "a" + HighSurrogate }),
            ("source", envelope with { Source = "/o" + LowSurrogate }),
            ("type", envelope with { Type = "t" + HighSurrogate + "x" }),
            ("subject", envelope with { Subject = new string([LowSurrogate, HighSurrogate]) }),
            ("dataschema", envelope with { DataSchema = "urn:" + HighSurrogate }),
            ("datacontenttype", envelope with { DataContentType = "text/plain" + HighSurrogate }),
            ("note", envelope.WithExtension("note", "n" + HighSurrogate)),
        ];

        foreach (var (attribute, message) in broken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://hooks.test/orders");
            var refused = Assert.Throws<FrameworkException>(() => CloudEventsHttpBinding.Write(message, request, CloudEventsContentMode.Binary));
            Assert.Equal(MessagingErrors.InvalidEnvelope, refused.Code);
            Assert.Contains($"attribute '{attribute}' has a lone surrogate", refused.Message, StringComparison.Ordinal);
            Assert.Equal(Assert.Throws<FrameworkException>(() => CloudEventsFormat.Encode(message)).Message, refused.Message);

            // 一个头也没写，请求体也没有：调用方拿到异常，不会发出半份请求
            Assert.Empty(request.Headers);
            Assert.Null(request.Content);
        }
    }

    [Fact]
    public void Write_BinaryMode_EncodesSurrogatePairs_AndAGenuineReplacementCharacter()
    {
        // 代理对是合法的 UTF-16，按 UTF-8 的四个字节编码；本来就是 U+FFFD 的字符照常编码，不当作孤立的代理项
        var envelope = new MessageEnvelope { Id = "1", Source = "/o", Type = "t", Subject = "订单 😀" }.WithExtension("note", ReplacementCharacter.ToString());
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://hooks.test/orders");

        CloudEventsHttpBinding.Write(envelope, request, CloudEventsContentMode.Binary);

        Assert.Equal("%E8%AE%A2%E5%8D%95%20%F0%9F%98%80", request.Headers.GetValues("ce-subject").Single());
        Assert.Equal("%EF%BF%BD", request.Headers.GetValues("ce-note").Single());
    }

    [Fact]
    public void EncodeHeaderValue_RefusesALoneSurrogate()
    {
        // 头值按 UTF-8 字节百分号编码，孤立的代理项没有 UTF-8 字节：不再悄悄编成 U+FFFD 的 %EF%BF%BD（ADR-0318）
        Assert.Throws<ArgumentException>(static () => CloudEventsHttpBinding.EncodeHeaderValue("a" + HighSurrogate));
        Assert.Throws<ArgumentException>(static () => CloudEventsHttpBinding.EncodeHeaderValue(LowSurrogate + "b"));
        Assert.Equal("%F0%9F%98%80", CloudEventsHttpBinding.EncodeHeaderValue("😀"));
    }
}
