// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives;
using Error = XiHan.Framework.Primitives.Error;

namespace XiHan.Framework.Plugins.OutOfProcess;

/// <summary>
/// 宿主回调服务 <c>xihan.plugin.v1.Host</c>：以插件身份经完整管道调用操作、经消息发布器发布消息；权限按清单授予判定（<c>spec/plugin/invocation.md</c> §5）。
/// </summary>
internal sealed class HostCallbackService(HostServices host) : Host.HostBase
{
    /// <inheritdoc />
    public override async Task<InvokeResponse> Invoke(InvokeRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        var runtime = RuntimeOf(context);
        Require(runtime, OutOfProcessPluginHost.PipelinePermission);

        await using var scope = host.Root.CreateAsyncScope();
        var invocation = PluginMetadata.CreateContext(scope.ServiceProvider, request.Metadata, runtime.Manifest.Id, context.CancellationToken);
        // 宿主以插件身份执行：主体 ID 为清单 ID、方案 Plugin、无声明；租户由 Tenant 阶段按 Baggage 解析
        invocation.SetSecurity(new SecurityContext(new Principal(runtime.Manifest.Id, OutOfProcessPluginHost.PluginScheme), null));
        var response = await scope.ServiceProvider.GetRequiredService<IOperationInvoker>().InvokeAsync(invocation, new OperationRequest(request.Operation, request.Payload.Memory)).ConfigureAwait(false);
        return new InvokeResponse
        {
            Status = PluginErrorMapper.ToStatus(response.Outcome),
            Payload = response.Outcome.IsSuccess ? ByteString.CopyFrom(response.Payload.Span) : ByteString.Empty,
        };
    }

    /// <inheritdoc />
    public override async Task<PublishResponse> Publish(PublishRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        var runtime = RuntimeOf(context);
        Require(runtime, OutOfProcessPluginHost.MessagingPermission);

        await using var scope = host.Root.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetService<IMessagePublisher>();
        var response = new PublishResponse();
        foreach (var envelope in request.Envelopes)
        {
            if (publisher is null)
            {
                response.Results.Add(PluginErrorMapper.ToStatus(Error.Unavailable(PluginErrors.Unavailable, PluginMessages.MessagingUnavailable, MessageArguments.Of(("id", runtime.Manifest.Id)))));
                continue;
            }

            if (!TopicName.IsValid(envelope.Topic))
            {
                response.Results.Add(PluginErrorMapper.ToStatus(Error.Validation(MessagingErrors.InvalidTopic, MessagingMessages.InvalidTopic, MessageArguments.Of(("topic", envelope.Topic)))));
                continue;
            }

            var converted = PluginEnvelopeMapper.FromProto(envelope);
            if (!converted.TryGetValue(out var message))
            {
                response.Results.Add(PluginErrorMapper.ToStatus(converted.Error!));
                continue;
            }

            response.Results.Add(PluginErrorMapper.ToStatus(await publisher.PublishAsync(envelope.Topic, message, context.CancellationToken).ConfigureAwait(false)));
        }

        return response;
    }

    /// <summary>
    /// 拦截器放进调用状态的运行时。
    /// </summary>
    private static PluginRuntime RuntimeOf(ServerCallContext context)
        => context.UserState.TryGetValue(SessionInterceptor.RuntimeKey, out var value) && value is PluginRuntime runtime
            ? runtime
            : throw new RpcException(new Grpc.Core.Status(StatusCode.Unauthenticated, "The plugin session token is missing or invalid."), new Metadata { { PluginProtocol.ErrorCodeTrailer, PluginErrors.CallbackDenied } });

    /// <summary>
    /// 权限未授予即以 PermissionDenied 结束调用，尾部元数据带错误码。
    /// </summary>
    private static void Require(PluginRuntime runtime, string permission)
    {
        if (!runtime.Permissions.IsGranted(permission))
        {
            throw new RpcException(new Grpc.Core.Status(StatusCode.PermissionDenied, $"Permission '{permission}' has not been granted to plugin '{runtime.Manifest.Id}'."), new Metadata { { PluginProtocol.ErrorCodeTrailer, PluginErrors.CallbackDenied } });
        }
    }
}
