// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Plugins.OutOfProcess.Runtime;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Plugins.Protocol.Hosting;
using XiHan.Framework.Plugins.Protocol.Mapping;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives;
using Error = XiHan.Framework.Primitives.Error;
using ProtoStatus = XiHan.Framework.Plugins.Protocol.V1.Status;

namespace XiHan.Framework.Plugins.OutOfProcess.Callbacks;

/// <summary>
/// 宿主回调服务 <c>xihan.plugin.v1.Host</c>
/// </summary>
/// <remarks>
/// 以插件身份经完整管道调用操作、经消息发布器发布消息；所需权限要在清单里声明并且已被宿主授予（<c>spec/plugin/invocation.md</c> §5）。依次核对会话令牌（拦截器）、权限、元数据的租户，然后执行。两者都按 <c>invocation.md</c> §2 从元数据建调用上下文，截止时间取元数据与 gRPC 调用自身截止时间中较早的
/// </remarks>
internal sealed class HostCallbackService(HostServices host) : Host.HostBase
{
    /// <inheritdoc />
    public override async Task<InvokeResponse> Invoke(InvokeRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        var runtime = RuntimeOf(context);
        Require(runtime, OutOfProcessPluginHost.PipelinePermission);

        // 权限之后判租户：插件身份没有租户声明，Tenant 阶段会照单采用元数据里的租户，只能在这里限定为在途调用的租户
        if (PluginCallbackTenants.Check(request.Metadata, runtime.Manifest.Id, runtime.IsHandlingTenant) is { } outside)
        {
            return new InvokeResponse { Status = PluginErrorMapper.ToStatus(outside) };
        }

        await using var scope = host.Root.CreateAsyncScope();
        var invocation = CreateContext(scope.ServiceProvider, request.Metadata, runtime, context);
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
        var statuses = new ProtoStatus[request.Envelopes.Count];
        var response = new PublishResponse();

        // 元数据的租户是整批的：越界即一项也不发布，每一项都是这个错误（invocation.md §5，ADR-0237）
        if (PluginCallbackTenants.Check(request.Metadata, runtime.Manifest.Id, runtime.IsHandlingTenant) is { } outside)
        {
            Array.Fill(statuses, PluginErrorMapper.ToStatus(outside));
            response.Results.AddRange(statuses);
            return response;
        }

        await using var scope = host.Root.CreateAsyncScope();
        // 在回调上下文里发布：发布器从当前调用补齐信封缺的租户、主体、关联 ID、区域与追踪（RFC-0009），与进程内的发布一致；
        // 以前不读元数据，插件处理某个租户的调用时发布的消息不带租户
        var invocation = CreateContext(scope.ServiceProvider, request.Metadata, runtime, context);
        using var current = PluginMetadata.Enter(invocation);
        var publisher = scope.ServiceProvider.GetService<IMessagePublisher>();
        // 逐项校验发布器、主题、信封自带的租户与转换，通过的按主题分组、每组一次批量发布；结果按原始位置回填，与请求的信封一一对应
        var batches = PluginPublishBatches.Collect(request.Envelopes, statuses, publisher is null ? Error.Unavailable(PluginErrors.Unavailable, PluginMessages.MessagingUnavailable, MessageArguments.Of(("id", runtime.Manifest.Id))) : null, envelope => PluginCallbackTenants.Check(envelope, runtime.Manifest.Id, runtime.IsHandlingTenant));
        foreach (var (topic, indexes, messages) in batches)
        {
            var results = await publisher!.PublishManyAsync(topic, messages, context.CancellationToken).ConfigureAwait(false);
            PluginPublishBatches.Scatter(statuses, indexes, results);
        }

        response.Results.AddRange(statuses);
        return response;
    }

    /// <summary>
    /// 回调的调用上下文
    /// </summary>
    /// <remarks>
    /// 宿主以插件身份执行：主体 ID 为清单 ID、方案 Plugin、无声明，元数据里的 <c>xihan.principal</c> 被覆盖；租户由 Tenant 阶段按 Baggage 解析（调用方已限定为在途调用的租户）。截止时间取元数据与 gRPC 调用自身截止时间中较早的（<c>invocation.md</c> §2）
    /// </remarks>
    private static InvocationContext CreateContext(IServiceProvider scopedServices, InvocationMetadata? metadata, PluginRuntime runtime, ServerCallContext call)
        => PluginIdentity.CreateContext(scopedServices, metadata, runtime.Manifest.Id, PluginDeadlines.FromCall(call.Deadline), call.CancellationToken);

    /// <summary>
    /// 拦截器放进调用状态的运行时
    /// </summary>
    private static PluginRuntime RuntimeOf(ServerCallContext context)
        => context.UserState.TryGetValue(SessionInterceptor.RuntimeKey, out var value) && value is PluginRuntime runtime
            ? runtime
            : throw new RpcException(new Grpc.Core.Status(StatusCode.Unauthenticated, "The plugin session token is missing or invalid."), new Metadata { { PluginProtocol.ErrorCodeTrailer, PluginErrors.CallbackDenied } });

    /// <summary>
    /// 权限未在清单声明或未授予即以 PermissionDenied 结束调用，尾部元数据带错误码
    /// </summary>
    /// <remarks>
    /// 授予多于声明的部分不放行（RFC-0017）
    /// </remarks>
    private static void Require(PluginRuntime runtime, string permission)
    {
        if (!PluginCallbackPermissions.IsAllowed(runtime.Manifest, runtime.Permissions, permission))
        {
            throw new RpcException(new Grpc.Core.Status(StatusCode.PermissionDenied, $"Permission '{permission}' is not both declared in the manifest of plugin '{runtime.Manifest.Id}' and granted to it."), new Metadata { { PluginProtocol.ErrorCodeTrailer, PluginErrors.CallbackDenied } });
        }
    }
}
