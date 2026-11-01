// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Protocols.CloudEvents.Tests;

/// <summary>
/// 在本机回环地址上监听的 Webhook 接收方。
/// </summary>
/// <remarks>
/// 用真实的 Kestrel 而不是测试服务器或替换的主处理器：出站请求要经过命名客户端真正的主处理器（追踪头由它写入），这里记录收到的请求头。
/// </remarks>
internal sealed class WebhookReceiver : IAsyncDisposable
{
    /// <summary>
    /// 接收方应用。
    /// </summary>
    private readonly WebApplication _app;

    /// <summary>
    /// 收到的每个请求的头（名字不区分大小写）。
    /// </summary>
    private readonly ConcurrentQueue<IReadOnlyDictionary<string, string>> _requests = new();

    /// <summary>
    /// 创建。
    /// </summary>
    private WebhookReceiver(WebApplication app)
    {
        _app = app;
    }

    /// <summary>
    /// 接收地址（<c>POST /hook</c>）。
    /// </summary>
    public Uri Url { get; private set; } = null!;

    /// <summary>
    /// 收到的请求头，按到达顺序。
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Requests => [.. _requests];

    /// <summary>
    /// 在随机端口上启动。
    /// </summary>
    public static async Task<WebhookReceiver> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        var receiver = new WebhookReceiver(app);
        app.MapPost("/hook", (HttpRequest request) =>
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, values) in request.Headers)
            {
                headers[name] = values.ToString();
            }

            receiver._requests.Enqueue(headers);
            return Results.Accepted();
        });
        await app.StartAsync();
        receiver.Url = new Uri(app.Urls.Single() + "/hook");
        return receiver;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
