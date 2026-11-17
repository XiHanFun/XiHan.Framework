// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Security.Cryptography;
using System.Text;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace EchoPlugin;

/// <summary>
/// 只接受带本次会话令牌的宿主调用
/// </summary>
/// <remarks>
/// 宿主每次调用都在元数据 <c>xihan-plugin-session</c> 里带 <c>XIHAN_PLUGIN_SESSION</c>；缺失或不匹配以 <c>Unauthenticated</c> 拒绝（<c>spec/plugin/handshake.md</c> §4，ADR-0202）。本机别的进程连上插件端点也冒充不了宿主。元数据里必须恰好一个值，带两个（即使其中一个正确）同样拒绝（ADR-0237）；校验先于一切处理，流式调用在读第一帧之前
/// </remarks>
internal sealed class SessionGuard : Interceptor
{
    /// <summary>
    /// 会话令牌的元数据键
    /// </summary>
    private const string SessionMetadataKey = "xihan-plugin-session";

    /// <summary>
    /// 本次会话的令牌
    /// </summary>
    private readonly byte[] _session = Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("XIHAN_PLUGIN_SESSION") ?? string.Empty);

    /// <inheritdoc />
    public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        Require(context);
        return continuation(request, context);
    }

    /// <inheritdoc />
    public override Task ServerStreamingServerHandler<TRequest, TResponse>(TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        Require(context);
        return continuation(request, responseStream, context);
    }

    /// <inheritdoc />
    public override Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, ServerCallContext context, ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        Require(context);
        return continuation(requestStream, context);
    }

    /// <inheritdoc />
    public override Task DuplexStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        Require(context);
        return continuation(requestStream, responseStream, context);
    }

    /// <summary>
    /// 令牌缺失或不匹配即以 <c>Unauthenticated</c> 结束调用
    /// </summary>
    private void Require(ServerCallContext context)
    {
        // GetValue 只取最后一个值：两个值里一个对一个错也能过，所以先数个数
        var values = context.RequestHeaders.Where(static entry => !entry.IsBinary && string.Equals(entry.Key, SessionMetadataKey, StringComparison.OrdinalIgnoreCase)).Select(static entry => entry.Value).ToList();
        var presented = values.Count == 1 ? values[0] : null;
        // 常数时间比较：按字节逐个比较会从耗时里泄露令牌前缀；没有令牌的环境（直接运行插件）一律拒绝
        if (_session.Length == 0 || presented is null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presented), _session))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "The plugin session token is missing or invalid."));
        }
    }
}
