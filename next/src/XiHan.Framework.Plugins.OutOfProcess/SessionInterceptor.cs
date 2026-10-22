// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Grpc.Core;
using Grpc.Core.Interceptors;

namespace XiHan.Framework.Plugins.OutOfProcess;

/// <summary>
/// 宿主回调服务的认证：每次调用必须带有效的 <c>xihan-plugin-session</c> 元数据，对应的运行时放进调用状态供服务取用。
/// </summary>
internal sealed class SessionInterceptor(PluginSessions sessions) : Interceptor
{
    /// <summary>
    /// 调用状态里存放运行时的键。
    /// </summary>
    public const string RuntimeKey = "xihan.plugin.runtime";

    /// <inheritdoc />
    public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(continuation);
        var token = context.RequestHeaders.GetValue(PluginProtocol.SessionMetadataKey);
        if (!sessions.TryResolve(token, out var runtime))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "The plugin session token is missing or invalid."), new Metadata { { PluginProtocol.ErrorCodeTrailer, PluginErrors.CallbackDenied } });
        }

        context.UserState[RuntimeKey] = runtime;
        return continuation(request, context);
    }
}
