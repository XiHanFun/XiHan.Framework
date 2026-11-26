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
/// binary 模式声明为 JSON 的数据照结构化 JSON 核对
/// </summary>
/// <remarks>
/// <c>spec/protocol/cloudevents.md</c>「入站」与「出站」（ADR-0323）：binary 的数据原样作请求体，以前 structured 会拒绝的数据（重复成员、嵌套过深、不是 JSON）照发照收，发布到远程代理时才在写出一侧失败
/// </remarks>
public sealed class CloudEventsBinaryDataTests
{
    /// <summary>
    /// 缺省选项
    /// </summary>
    private static readonly CloudEventsProtocolOptions _options = new();

    /// <summary>
    /// structured 写不出、读不进的 JSON 数据
    /// </summary>
    /// <remarks>
    /// 重复成员（含嵌套里的）、嵌进文档之后超过 64 层、不是 JSON、不成对的代理项转义
    /// </remarks>
    public static TheoryData<string> UnwritableData =>
    [
        """{"a":1,"a":2}""",
        """{"order":{"id":1,"id":2}}""",
        Nested(64),
        "nope",
        "\"\\ud800\"",
    ];

    /// <summary>
    /// 嵌套 <paramref name="levels"/> 层的数组
    /// </summary>
    private static string Nested(int levels) => new string('[', levels) + new string(']', levels);

    /// <summary>
    /// 构造一个 binary 模式的请求
    /// </summary>
    private static HttpRequest BinaryRequest(string? contentType, string body)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        if (contentType is not null)
        {
            context.Request.ContentType = contentType;
        }

        context.Request.Headers["ce-specversion"] = "1.0";
        context.Request.Headers["ce-id"] = "evt-1";
        context.Request.Headers["ce-source"] = "/orders";
        context.Request.Headers["ce-type"] = "orders.placed";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return context.Request;
    }

    /// <summary>
    /// 构造一个 structured 模式的请求，数据内联在 <c>data</c>
    /// </summary>
    private static HttpRequest StructuredRequest(string data)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.ContentType = CloudEventsHttpBinding.StructuredContentType;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes($$"""{"specversion":"1.0","id":"evt-1","source":"/orders","type":"orders.placed","data":{{data}}}"""));
        return context.Request;
    }

    [Theory]
    [MemberData(nameof(UnwritableData))]
    public void Write_BinaryMode_RefusesJsonData_StructuredCannotWrite_BeforeWritingAnyHeader(string data)
    {
        var envelope = new MessageEnvelope { Id = "evt-1", Source = "/orders", Type = "orders.placed", Data = Encoding.UTF8.GetBytes(data) };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://hooks.test/orders");

        var structured = Assert.Throws<FrameworkException>(() => CloudEventsFormat.Encode(envelope));
        var refused = Assert.Throws<FrameworkException>(() => CloudEventsHttpBinding.Write(envelope, request, CloudEventsContentMode.Binary));

        // 同一个信封换一种内容模式，调用方拿到的异常相同；一个头也没写，请求体也没有
        Assert.Equal(MessagingErrors.InvalidEnvelope, refused.Code);
        Assert.Equal(structured.Message, refused.Message);
        Assert.Empty(request.Headers);
        Assert.Null(request.Content);
    }

    [Fact]
    public async Task Write_BinaryMode_SendsWritableJsonData_AndNonJsonData_AsIs()
    {
        // 63 层的数据嵌进文档是 64 层，写得出；不是 JSON 的内容类型不按 JSON 核对，数据原样作请求体
        var deepest = new MessageEnvelope { Id = "evt-1", Source = "/orders", Type = "orders.placed", Data = Encoding.UTF8.GetBytes(Nested(63)) };
        var text = deepest with { DataContentType = "text/plain", Data = """{"a":1,"a":2}"""u8.ToArray() };
        var empty = deepest with { Data = default };

        foreach (var envelope in new[] { deepest, text, empty })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://hooks.test/orders");
            CloudEventsHttpBinding.Write(envelope, request, CloudEventsContentMode.Binary);
            Assert.Equal(envelope.Data.ToArray(), await request.Content!.ReadAsByteArrayAsync());
        }
    }

    [Theory]
    [MemberData(nameof(UnwritableData))]
    public async Task Read_BinaryMode_RejectsJsonData_LikeStructuredRejectsTheSameEvent(string data)
    {
        var structured = (await CloudEventsHttpBinding.ReadAsync(StructuredRequest(data), _options, CancellationToken.None)).Error!;
        var binary = (await CloudEventsHttpBinding.ReadAsync(BinaryRequest("application/json", data), _options, CancellationToken.None)).Error;

        // 错误码、消息键与位置都与 structured 读同一个事件时相同：位置按数据在结构化文档里的 data 算
        Assert.Equal((MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalidJson), (structured.Code, structured.MessageKey));
        Assert.Equal((structured.Code, structured.MessageKey, structured.Details!["reason"]), (binary?.Code, binary?.MessageKey, binary?.Details!["reason"]));
    }

    [Fact]
    public async Task InvalidUtf8InJsonData_IsRefusedOnWrite_AndRejectedOnRead_InBinaryMode()
    {
        // 字符串里不合法的 UTF-8 过不了 RFC-0001「JSON 文本」的共同检查：structured 读同一个事件时拒绝，binary 两个方向都照它
        byte[] data = [(byte)'{', (byte)'"', (byte)'a', (byte)'"', (byte)':', (byte)'"', 0xED, 0xA0, 0x80, (byte)'"', (byte)'}'];
        var envelope = new MessageEnvelope { Id = "evt-1", Source = "/orders", Type = "orders.placed", Data = data };
        using var outgoing = new HttpRequestMessage(HttpMethod.Post, "https://hooks.test/orders");
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.ContentType = "application/json";
        context.Request.Headers["ce-specversion"] = "1.0";
        context.Request.Headers["ce-id"] = "evt-1";
        context.Request.Headers["ce-source"] = "/orders";
        context.Request.Headers["ce-type"] = "orders.placed";
        context.Request.Body = new MemoryStream(data);

        var refused = Assert.Throws<FrameworkException>(() => CloudEventsHttpBinding.Write(envelope, outgoing, CloudEventsContentMode.Binary));
        var rejected = (await CloudEventsHttpBinding.ReadAsync(context.Request, _options, CancellationToken.None)).Error;

        Assert.Equal(Assert.Throws<FrameworkException>(() => CloudEventsFormat.Encode(envelope)).Message, refused.Message);
        Assert.Empty(outgoing.Headers);
        Assert.Equal((MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalidJson, "$"), (rejected?.Code, rejected?.MessageKey, rejected?.Details!["reason"]));
    }

    [Fact]
    public async Task Read_BinaryMode_RejectsDataThatIsNotOneJsonValue_EvenIfItWouldFitAsMembers()
    {
        // 「1,"other":2」包进 data 成员之后是合法的对象，单独却不是一个 JSON 值：照样不合法
        var error = (await CloudEventsHttpBinding.ReadAsync(BinaryRequest("application/json", "1,\"other\":2"), _options, CancellationToken.None)).Error;

        Assert.Equal((MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalidJson, "$"), (error?.Code, error?.MessageKey, error?.Details!["reason"]));
    }

    [Fact]
    public async Task Read_BinaryMode_ReportsTheDuplicateMember_AtItsPathInTheStructuredDocument()
    {
        var error = (await CloudEventsHttpBinding.ReadAsync(BinaryRequest("application/json", """{"order":{"id":1,"id":2}}"""), _options, CancellationToken.None)).Error;

        Assert.Equal("$.data.order.id", error?.Details!["reason"]);
    }

    [Theory]
    [InlineData("application/json; charset=utf-8")]
    [InlineData("application/merge-patch+json")]
    [InlineData(null)]
    public async Task Read_BinaryMode_ChecksEveryJsonContentType_IncludingTheDefault(string? contentType)
    {
        // Content-Type 缺省时数据内容类型是 application/json，同样核对
        var error = (await CloudEventsHttpBinding.ReadAsync(BinaryRequest(contentType, """{"a":1,"a":2}"""), _options, CancellationToken.None)).Error;

        Assert.Equal((MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalidJson), (error?.Code, error?.MessageKey));
    }

    [Fact]
    public async Task Read_BinaryMode_AcceptsWritableJsonData_NonJsonData_AndAnEmptyBody()
    {
        var deepest = Assert.Single((await CloudEventsHttpBinding.ReadAsync(BinaryRequest("application/json", Nested(63)), _options, CancellationToken.None)).ValueOrThrow());
        var text = Assert.Single((await CloudEventsHttpBinding.ReadAsync(BinaryRequest("text/plain", "nope"), _options, CancellationToken.None)).ValueOrThrow());
        var empty = Assert.Single((await CloudEventsHttpBinding.ReadAsync(BinaryRequest("application/json", string.Empty), _options, CancellationToken.None)).ValueOrThrow());

        // 读出的信封都写得成结构化 JSON；数据照原样
        Assert.Equal(Nested(63), Encoding.UTF8.GetString(deepest.Data.Span));
        Assert.Equal("nope", Encoding.UTF8.GetString(text.Data.Span));
        Assert.True(empty.Data.IsEmpty);
        foreach (var envelope in new[] { deepest, text, empty })
        {
            EnvelopeAssert.Same(envelope, CloudEventsFormat.Decode(CloudEventsFormat.Encode(envelope)).ValueOrThrow());
        }
    }
}
