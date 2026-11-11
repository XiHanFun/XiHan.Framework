// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Protocols.Http.Routing;
using XiHan.Framework.Protocols.Http.Tests.Fixtures;
using XiHan.Framework.Security.InMemory;

namespace XiHan.Framework.Protocols.Http.Tests;

/// <summary>
/// 操作的请求体在授权之后才读，并有上限与类型要求
/// </summary>
/// <remarks>
/// 安全审查 S-07（ADR-0176）：请求体原先在进管道之前就整读、解析，没有框架上限、不看类型，匿名调用方也能让宿主为每个请求缓冲几十 MB
/// </remarks>
public sealed class OperationPayloadBindingTests
{
    /// <summary>
    /// 持有 <c>orders.write</c> 的主体的 API Key
    /// </summary>
    private const string WriterKey = "writer-key-0123456789";

    /// <summary>
    /// 启动带内存授权与示例订单特性的宿主，请求体上限为 <paramref name="maxBody"/>
    /// </summary>
    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(long maxBody)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(
        [
            new("XiHan:Security:InMemory:Principals:writer:ApiKey", WriterKey),
            new("XiHan:Security:InMemory:Grants:user:writer", "orders.write"),
            new("XiHan:Protocols:Http:MaxRequestBodyBytes", maxBody.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ]);
        builder.AddXiHan(static xihan => xihan.UseFeature<HttpProtocolFeature>().UseFeature<InMemorySecurityFeature>().UseFeature<SampleOrdersFeature>());
        var app = builder.Build();
        app.UseXiHan();
        app.MapXiHanOperations();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    [Fact]
    public async Task AnonymousCaller_IsRejectedByAuthorization_BeforeTheBodyIsRead()
    {
        var (app, client) = await StartAsync(HttpProtocolOptions.DefaultMaxRequestBodyBytes);
        await using var _ = app;

        // 请求体本身不合法：原先先解析、回 400；现在授权在前，匿名直接 401，请求体不读
        using var malformed = await PostAsync(client, new StringContent("{not json", Encoding.UTF8, "application/json"), null);
        Assert.Equal(HttpStatusCode.Unauthorized, malformed.StatusCode);

        using var oversized = await PostAsync(client, Json(new string('x', 8 * 1024 * 1024)), null);
        Assert.Equal(HttpStatusCode.Unauthorized, oversized.StatusCode);
    }

    [Fact]
    public async Task BodyOverTheLimit_IsRejected_WithOrWithoutContentLength()
    {
        var (app, client) = await StartAsync(256);
        await using var _ = app;

        using (var declared = await PostAsync(client, Json(new string('x', 1024)), WriterKey))
        {
            Assert.Equal(HttpStatusCode.BadRequest, declared.StatusCode);
            Assert.Equal(HttpErrors.RequestBodyTooLarge, await CodeOf(declared));
        }

        // 没有 Content-Length（分块）：读到上限即停
        var content = new StreamContent(new UnknownLengthStream(Encoding.UTF8.GetBytes("""{"name":""" + "\"" + new string('x', 1024) + "\"}")));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using (var chunked = await PostAsync(client, content, WriterKey))
        {
            Assert.Equal(HttpStatusCode.BadRequest, chunked.StatusCode);
            Assert.Equal(HttpErrors.RequestBodyTooLarge, await CodeOf(chunked));
        }

        using var small = await PostAsync(client, Json("ok"), WriterKey);
        Assert.Equal(HttpStatusCode.Created, small.StatusCode);
    }

    [Fact]
    public async Task NonJsonContentType_IsRejected()
    {
        var (app, client) = await StartAsync(HttpProtocolOptions.DefaultMaxRequestBodyBytes);
        await using var _ = app;

        // text/plain 是浏览器跨站也能发的简单请求：内容是 JSON 也不受理
        using (var plain = await PostAsync(client, new StringContent("""{"name":"x"}""", Encoding.UTF8, "text/plain"), WriterKey))
        {
            Assert.Equal(HttpStatusCode.BadRequest, plain.StatusCode);
            Assert.Equal(HttpErrors.RequestBodyNotJson, await CodeOf(plain));
        }

        using var merge = await PostAsync(client, new StringContent("""{"name":"x"}""", Encoding.UTF8, "application/merge-patch+json"), WriterKey);
        Assert.Equal(HttpStatusCode.Created, merge.StatusCode);
    }

    /// <summary>
    /// 名字为 <paramref name="name"/> 的 JSON 请求体
    /// </summary>
    private static StringContent Json(string name)
        => new(JsonSerializer.Serialize(new { name }), Encoding.UTF8, "application/json");

    /// <summary>
    /// 以给定 API Key（空为匿名）调用 <c>orders.create</c>
    /// </summary>
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, HttpContent content, string? apiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/orders") { Content = content };
        if (apiKey is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(ApiKeyAuthenticationHandler.SchemeName, apiKey);
        }

        return await client.SendAsync(request);
    }

    /// <summary>
    /// Problem Details 里的错误码
    /// </summary>
    private static async Task<string?> CodeOf(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    /// <summary>
    /// 不报长度的只读流
    /// </summary>
    /// <remarks>
    /// 让客户端按分块发送，请求没有 <c>Content-Length</c>
    /// </remarks>
    private sealed class UnknownLengthStream(byte[] data) : MemoryStream(data)
    {
        /// <inheritdoc />
        public override bool CanSeek => false;
    }
}
