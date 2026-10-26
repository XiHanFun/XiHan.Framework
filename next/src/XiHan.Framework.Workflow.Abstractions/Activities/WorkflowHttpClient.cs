// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// <c>Http</c> 活动用的 HTTP 客户端（单例，连接池寿命 2 分钟以跟随 DNS 变化）；应用可在装配前注册自己的实例替换（如带代理、认证处理器）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowHttpClient : IDisposable
{
    /// <summary>
    /// 底层客户端。
    /// </summary>
    private readonly HttpClient _client;

    /// <summary>
    /// 以默认处理器创建。
    /// </summary>
    public WorkflowHttpClient()
        : this(new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) }, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan })
    {
    }

    /// <summary>
    /// 以给定客户端创建（超时由活动按属性控制，客户端自身的超时应为无限）。
    /// </summary>
    public WorkflowHttpClient(HttpClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// 发送请求；超过 <paramref name="timeout"/> 以 <see cref="OperationCanceledException"/> 结束。
    /// </summary>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);
        return await _client.SendAsync(request, HttpCompletionOption.ResponseContentRead, linked.Token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();
}
