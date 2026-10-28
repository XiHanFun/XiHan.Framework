// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Text.Json;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.Grpc.Proto;
using XiHan.Framework.Protocols.Grpc.Services;

namespace XiHan.Framework.Protocols.Grpc.Methods;

/// <summary>
/// 把操作表绑定为 gRPC 一元方法：消息以字节数组进出，处理器里按 Schema 在 protobuf 与 JSON 之间转换，
/// 以请求头构造的调用上下文经 <see cref="IOperationInvoker.InvokeAsync(InvocationContext, OperationRequest)"/> 走完整内核管道；失败映射为 gRPC 状态，错误码与 trace-id 放在尾部元数据。
/// 端点带 <see cref="PipelineBypassMetadata"/>：响应完全由 gRPC 处理器写出，不经 <c>UseXiHan()</c> 的 Problem Details 回写。
/// </summary>
internal sealed class OperationMethodProvider : IServiceMethodProvider<OperationsGrpcService>
{
    /// <summary>
    /// 尾部元数据：框架错误码。
    /// </summary>
    private const string ErrorCodeTrailer = "xihan-error-code";

    /// <summary>
    /// 尾部元数据：trace-id。
    /// </summary>
    private const string TraceIdTrailer = "xihan-trace-id";

    /// <summary>
    /// 字节数组直通的编组器：protobuf 的编解码在处理器里按 Schema 完成。
    /// </summary>
    private static readonly Marshaller<byte[]> _bytes = Marshallers.Create<byte[]>(static (bytes, context) => context.Complete(bytes), static context => context.PayloadAsNewBuffer());

    /// <summary>
    /// 操作表。
    /// </summary>
    private readonly GrpcOperationTable _table;

    /// <summary>
    /// 应用；提供服务状态。
    /// </summary>
    private readonly App _app;

    /// <summary>
    /// 操作调用器。
    /// </summary>
    private readonly IOperationInvoker _invoker;

    /// <summary>
    /// 本地化器；未装配本地化时为空，状态文案按进程默认区域。
    /// </summary>
    private readonly IMessageLocalizer? _localizer;

    /// <summary>
    /// 创建。
    /// </summary>
    public OperationMethodProvider(GrpcOperationTable table, App app, IOperationInvoker invoker, IMessageLocalizer? localizer)
    {
        _table = table ?? throw new ArgumentNullException(nameof(table));
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
        _localizer = localizer;
    }

    /// <inheritdoc />
    public void OnServiceMethodDiscovery(ServiceMethodProviderContext<OperationsGrpcService> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (var operation in _table.Operations)
        {
            var method = new Method<byte[], byte[]>(MethodType.Unary, operation.Method.FullServiceName(_table.Package), operation.Method.Method, _bytes, _bytes);
            // 端点元数据带操作描述（供诊断与工具链），并绕过宿主管道：每次调用在处理器里以自己的上下文走完整管道
            context.AddUnaryMethod(method, [operation.Descriptor, PipelineBypassMetadata.Instance], (_, request, call) => InvokeAsync(operation, request, call));
        }
    }

    /// <summary>
    /// 处理一次一元调用。
    /// </summary>
    private async Task<byte[]> InvokeAsync(GrpcOperation operation, byte[] request, ServerCallContext call)
    {
        // 未处于服务状态时还没有调用上下文：直接以 Unavailable 拒绝
        if (!_app.State.IsServing())
        {
            throw new RpcException(new Status(StatusCode.Unavailable, KernelMessages.NotServing.L(("state", _app.State.ToString()))));
        }

        var httpContext = call.GetHttpContext();
        var context = HttpInvocation.CreateContext(httpContext);
        // 协议与请求目标改记 gRPC 方法全名；远端地址与 User-Agent 沿用承载它的 HTTP/2 请求
        context.SetOrigin(context.GetOrigin().ValueOrThrow() with { Protocol = InvocationProtocols.Grpc, Target = call.Method });
        call.ResponseTrailers.Add(TraceIdTrailer, context.TraceId);

        var json = new ArrayBufferWriter<byte>();
        try
        {
            using var writer = new Utf8JsonWriter(json);
            ProtoDecoder.Decode(operation.Request, request, writer);
        }
        catch (ProtoException exception)
        {
            throw Fail(call, context, Error.Validation(GrpcErrors.RequestMalformed, GrpcMessages.RequestMalformed, MessageArguments.Of(("operation", operation.Descriptor.Id), ("reason", exception.Message))));
        }

        var response = await _invoker.InvokeAsync(context, new OperationRequest(operation.Descriptor.Id, json.WrittenMemory)).ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            throw Fail(call, context, response.Error!);
        }

        if (response.Payload.Length == 0)
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(response.Payload);
            var output = new ArrayBufferWriter<byte>();
            ProtoEncoder.Encode(operation.Response, document.RootElement, output);
            return output.WrittenMemory.ToArray();
        }
        catch (ProtoException exception)
        {
            // 响应与 Schema 不符是服务端的编程错误：Internal，不把细节暴露给调用方，原因留在错误详情里供日志
            throw Fail(call, context, Error.Unexpected(GrpcErrors.ResponseUnencodable, GrpcMessages.ResponseUnencodable, details: MessageArguments.Of(("operation", operation.Descriptor.Id), ("reason", exception.Message))));
        }
    }

    /// <summary>
    /// 把失败结果翻译为 gRPC 异常：状态码按错误 KIND，文案按调用方区域，错误码进尾部元数据。
    /// </summary>
    private RpcException Fail(ServerCallContext call, InvocationContext context, Error error)
    {
        var detail = _localizer is not null ? _localizer.Localize(error, context) : error.Message;
        call.ResponseTrailers.Add(ErrorCodeTrailer, error.Code);
        return new RpcException(new Status(GrpcStatusMapping.StatusCodeFor(error), detail));
    }
}
