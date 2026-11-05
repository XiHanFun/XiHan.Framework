// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Samples.Tests.Fixtures;

namespace XiHan.Framework.Samples.Tests;

/// <summary>
/// 真正运行 SimpleApp，验证 README 列出的命令
/// </summary>
/// <remarks>
/// 只发布不运行发现不了配置与端点声明的问题，比如 <c>/openapi.json</c> 一直返回 500
/// </remarks>
public sealed class SimpleAppTests(SimpleAppProcess app) : IClassFixture<SimpleAppProcess>
{
    [Fact]
    public async Task OpenApiDocument_IsPublished_AndDescribesTheOperations()
    {
        using var response = await app.Client.GetAsync(new Uri("/openapi.json", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var paths = document.RootElement.GetProperty("paths");
        Assert.Equal("orders.create", paths.GetProperty("/orders").GetProperty("post").GetProperty("operationId").GetString());
        Assert.Equal("orders.list", paths.GetProperty("/orders").GetProperty("get").GetProperty("operationId").GetString());

        // 这几个普通端点的响应不是操作契约，文档不为它们另推 Schema
        Assert.False(paths.TryGetProperty("/greet", out _));
        Assert.False(paths.TryGetProperty("/whoami", out _));
        Assert.False(paths.TryGetProperty("/orders/stats", out _));
    }

    [Fact]
    public async Task EventsEndpoint_OnlyAcceptsTheServiceAccount()
    {
        var before = await HandledAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, await DeliverAsync(apiKey: null));
        Assert.Equal(HttpStatusCode.Forbidden, await DeliverAsync("dev-alice-key"));
        Assert.Equal(HttpStatusCode.Forbidden, await DeliverAsync("dev-bob-key"));
        Assert.Equal(before, await HandledAsync());

        // 事件带的租户是发送方给的，只有持有 events.deliver 的服务账号能这样投递（ADR-0121）
        Assert.Equal(HttpStatusCode.Accepted, await DeliverAsync("dev-relay-key"));
        Assert.Equal(before + 1, await HandledAsync());
    }

    /// <summary>
    /// 投递一个带租户的 binary 模式事件
    /// </summary>
    private async Task<HttpStatusCode> DeliverAsync(string? apiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/events", UriKind.Relative))
        {
            Content = new StringContent("""{"id":"evt-1"}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("ce-specversion", "1.0");
        request.Headers.Add("ce-id", Guid.NewGuid().ToString("N"));
        request.Headers.Add("ce-source", "/external");
        request.Headers.Add("ce-type", "orders.placed");
        request.Headers.Add("ce-xihantenant", "acme");
        if (apiKey is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"ApiKey {apiKey}");
        }

        using var response = await app.Client.SendAsync(request, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    /// <summary>
    /// 消息处理器到目前为止处理的事件数
    /// </summary>
    private async Task<int> HandledAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/orders/stats", UriKind.Relative));
        request.Headers.TryAddWithoutValidation("Authorization", "ApiKey dev-alice-key");
        using var response = await app.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.GetProperty("handled").GetInt32();
    }
}
