// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Hosting.AspNetCore.Tests.Fixtures;

namespace XiHan.Framework.Hosting.AspNetCore.Tests;

/// <summary>
/// 限长读取请求体：HTTP、MCP 与 CloudEvents 三个入口共用的一份
/// </summary>
/// <remarks>
/// 原先三个协议包各写一遍，其中两份不压服务器的上限、也不接服务器报的 413，超限时答复的不是协议自己的错误格式
/// </remarks>
public sealed class RequestBodyLimitTests
{
    [Fact]
    public async Task Body_WithinTheLimit_IsReadWhole_AndAnEmptyBodyIsAnEmptyArray()
    {
        var exact = await HttpInvocation.ReadBodyAsync(Context(new byte[64], declareLength: true), 64, CancellationToken.None);
        var empty = await HttpInvocation.ReadBodyAsync(Context([], declareLength: false), 64, CancellationToken.None);

        Assert.Equal(64, exact.ValueOrThrow().Length);
        Assert.Empty(empty.ValueOrThrow());
    }

    [Fact]
    public async Task DeclaredLength_OverTheLimit_IsNotReadAtAll()
    {
        var body = new CountingStream(new byte[65]);
        var context = Context(body, contentLength: 65);

        var read = await HttpInvocation.ReadBodyAsync(context, 64, CancellationToken.None);

        Assert.False(read.HasValue);
        Assert.Equal(0, body.BytesRead);
    }

    [Fact]
    public async Task UndeclaredLength_OverTheLimit_StopsAtTheFirstChunkThatCrossesIt()
    {
        // 分块、HTTP/2 没有 Content-Length：边读边计数，不会把整个请求体读进内存
        var body = new CountingStream(new byte[1024 * 1024]);
        var context = Context(body, contentLength: null);

        var read = await HttpInvocation.ReadBodyAsync(context, 1000, CancellationToken.None);

        Assert.False(read.HasValue);
        Assert.InRange(body.BytesRead, 1001, 64 * 1024);
    }

    [Theory]
    [InlineData(null, 64L)]
    [InlineData(1_000_000L, 64L)]
    [InlineData(32L, 32L)]
    public async Task ServerLimit_IsLoweredToTheCallersLimit_ButNeverRaised(long? server, long expected)
    {
        var context = Context(new byte[8], declareLength: true);
        var feature = new BodySizeFeature { MaxRequestBodySize = server };
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(feature);

        await HttpInvocation.ReadBodyAsync(context, 64, CancellationToken.None);

        Assert.Equal(expected, feature.MaxRequestBodySize);
    }

    [Fact]
    public async Task ServerRejectingTheBodyHalfway_CountsAsOverTheLimit_AndOtherFailuresPropagate()
    {
        // 服务器自己的上限比调用方的小：读到一半抛 413，调用方按超过处理，用自己的错误格式答复
        var tooLarge = Context(new ThrowingStream(new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge)), contentLength: null);
        var malformed = Context(new ThrowingStream(new BadHttpRequestException("Bad chunk.", StatusCodes.Status400BadRequest)), contentLength: null);

        Assert.False((await HttpInvocation.ReadBodyAsync(tooLarge, 1024, CancellationToken.None)).HasValue);
        await Assert.ThrowsAsync<BadHttpRequestException>(async () => await HttpInvocation.ReadBodyAsync(malformed, 1024, CancellationToken.None));
    }

    [Fact]
    public async Task Arguments_AreChecked()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(static async () => await HttpInvocation.ReadBodyAsync(null!, 1, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(static async () => await HttpInvocation.ReadBodyAsync(new DefaultHttpContext(), -1, CancellationToken.None));
    }

    /// <summary>
    /// 带这些字节作请求体的请求
    /// </summary>
    private static DefaultHttpContext Context(byte[] body, bool declareLength)
        => Context(new MemoryStream(body), declareLength ? body.Length : null);

    /// <summary>
    /// 带这个流作请求体的请求
    /// </summary>
    private static DefaultHttpContext Context(Stream body, long? contentLength)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = body;
        context.Request.ContentLength = contentLength;
        return context;
    }
}
