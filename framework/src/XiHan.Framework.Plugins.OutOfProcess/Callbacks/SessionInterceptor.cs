// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Grpc.Core;
using Grpc.Core.Interceptors;
using XiHan.Framework.Plugins.Protocol;

namespace XiHan.Framework.Plugins.OutOfProcess.Callbacks;

/// <summary>
/// 宿主回调服务的认证
/// </summary>
/// <remarks>
/// 每次调用必须恰好带一个有效的 <c>xihan-plugin-session</c> 元数据，对应的运行时放进调用状态供服务取用；没有或带了两个及以上（即使其中一个正确）都按不匹配拒绝（<c>handshake.md</c> §4，ADR-0237）
/// </remarks>
internal sealed class SessionInterceptor(PluginSessions sessions) : Interceptor
{
    /// <summary>
    /// 调用状态里存放运行时的键
    /// </summary>
    public const string RuntimeKey = "xihan.plugin.runtime";

    /// <inheritdoc />
    public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(continuation);
        // GetValue 只取最后一个：带两个值时一个对一个错也能过，所以先数个数
        string? token = null;
        var count = 0;
        foreach (var entry in context.RequestHeaders)
        {
            if (string.Equals(entry.Key, PluginProtocol.SessionMetadataKey, StringComparison.OrdinalIgnoreCase) && !entry.IsBinary)
            {
                token = entry.Value;
                count++;
            }
        }

        if (count != 1 || !sessions.TryResolve(token, out var runtime))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "The plugin session token is missing or invalid."), new Metadata { { PluginProtocol.ErrorCodeTrailer, PluginErrors.CallbackDenied } });
        }

        context.UserState[RuntimeKey] = runtime;
        return continuation(request, context);
    }
}
