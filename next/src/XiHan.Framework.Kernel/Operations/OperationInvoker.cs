// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// <see cref="IOperationInvoker"/> 的内核实现：按登记表找到描述，经 <see cref="App.InvokeAsync"/> 走完整管道，或在当前调用里直接执行。
/// </summary>
internal sealed class OperationInvoker : IOperationInvoker
{
    /// <summary>
    /// 应用；提供完整管道。
    /// </summary>
    private readonly App _app;

    /// <summary>
    /// 操作登记表。
    /// </summary>
    private readonly OperationRegistry _registry;

    /// <summary>
    /// 创建调用器。
    /// </summary>
    public OperationInvoker(App app, OperationRegistry registry)
    {
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <inheritdoc />
    public async ValueTask<OperationResponse> InvokeAsync(InvocationContext context, OperationRequest request)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        if (!_registry.Find(request.Id).TryGetValue(out var descriptor))
        {
            return OperationResponse.Failure(Unknown(request.Id));
        }

        // 描述与载荷在管道开始前进入上下文：Authorize 阶段按契约判定，Validate 阶段按 Schema 校验载荷并解码
        context.SetOperation(descriptor);
        context.SetOperationPayload(request.Payload);
        var outcome = await _app.InvokeAsync(context, ctx => DispatchAsync(ctx, descriptor, request.Payload)).ConfigureAwait(false);
        return ToResponse(context, descriptor, outcome);
    }

    /// <inheritdoc />
    public async ValueTask<OperationResponse> ExecuteAsync(InvocationContext context, OperationRequest request)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        if (!_registry.Find(request.Id).TryGetValue(out var descriptor))
        {
            var unknown = Unknown(request.Id);
            context.SetOutcome(unknown);
            return OperationResponse.Failure(unknown);
        }

        context.SetOperation(descriptor);
        await DispatchAsync(context, descriptor, request.Payload).ConfigureAwait(false);
        return ToResponse(context, descriptor, context.GetOutcome().ValueOr(Result.Success()));
    }

    /// <inheritdoc />
    public async ValueTask<Result<TResponse>> InvokeAsync<TRequest, TResponse>(InvocationContext context, string operationId, TRequest request)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        if (!TryResolveTyped<TRequest, TResponse>(operationId, out var descriptor, out var error))
        {
            return error;
        }

        // 类型化调用没有载荷：请求对象先进上下文，Validate 阶段按序列化元数据编码一次再校验
        context.SetOperation(descriptor);
        context.SetOperationRequest(request);
        var outcome = await _app.InvokeAsync(context, ctx => ExecuteTypedAsync(ctx, descriptor, request)).ConfigureAwait(false);
        return ToTypedResult<TResponse>(context, descriptor, outcome);
    }

    /// <inheritdoc />
    public async ValueTask<Result<TResponse>> ExecuteAsync<TRequest, TResponse>(InvocationContext context, string operationId, TRequest request)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        if (!TryResolveTyped<TRequest, TResponse>(operationId, out var descriptor, out var error))
        {
            context.SetOutcome(error);
            return error;
        }

        context.SetOperation(descriptor);
        await ExecuteTypedAsync(context, descriptor, request).ConfigureAwait(false);
        return ToTypedResult<TResponse>(context, descriptor, context.GetOutcome().ValueOr(Result.Success()));
    }

    /// <summary>
    /// Handle 阶段的终点：Validate 阶段已解码的请求直接执行，否则（没有内置中间件或直接 ExecuteAsync）在这里解码；把结果与响应对象写进上下文。
    /// </summary>
    private static async ValueTask DispatchAsync(InvocationContext context, OperationDescriptor descriptor, ReadOnlyMemory<byte> payload)
    {
        if (!context.GetOperationRequest().TryGetValue(out var request))
        {
            var decoded = descriptor.DecodeRequest(payload);
            if (!decoded.TryGetValue(out request))
            {
                context.SetOutcome(decoded.Error!);
                return;
            }

            context.SetOperationRequest(request);
        }

        await ExecuteTypedAsync(context, descriptor, request).ConfigureAwait(false);
    }

    /// <summary>
    /// 执行已解码的请求并记录结果：成功时响应对象进上下文，失败时错误成为调用结果。
    /// </summary>
    private static async ValueTask ExecuteTypedAsync(InvocationContext context, OperationDescriptor descriptor, object request)
    {
        var result = await descriptor.ExecuteAsync(context, request).ConfigureAwait(false);
        if (result.TryGetValue(out var response))
        {
            context.SetOperationResponse(response);
            context.SetOutcome(Result.Success());
        }
        else
        {
            context.SetOutcome(result.Error!);
        }
    }

    /// <summary>
    /// 按类型参数校验描述：请求与响应类型必须与登记的一致。
    /// </summary>
    private bool TryResolveTyped<TRequest, TResponse>(string operationId, out OperationDescriptor descriptor, out Error error)
    {
        ArgumentNullException.ThrowIfNull(operationId);
        if (!_registry.Find(operationId).TryGetValue(out var found))
        {
            descriptor = null!;
            error = Unknown(operationId);
            return false;
        }

        descriptor = found;
        if (found.RequestType != typeof(TRequest) || found.ResponseType != typeof(TResponse))
        {
            error = new Error(
                KernelErrors.OperationTypeMismatch,
                KernelMessages.OperationTypeMismatch,
                MessageArguments.Of(("id", operationId), ("expected", $"{found.RequestType.Name} -> {found.ResponseType.Name}"), ("actual", $"{typeof(TRequest).Name} -> {typeof(TResponse).Name}")));
            return false;
        }

        error = null!;
        return true;
    }

    /// <summary>
    /// 把调用结果与上下文里的响应对象组装成 XGI 响应。
    /// </summary>
    private static OperationResponse ToResponse(InvocationContext context, OperationDescriptor descriptor, Result outcome)
    {
        // 管道可能在到达 Handle 之前就以失败结束（鉴权、校验、超时），此时没有响应对象
        if (outcome.IsFailure || !context.GetOperationResponse().TryGetValue(out var response))
        {
            return new OperationResponse(outcome, ReadOnlyMemory<byte>.Empty);
        }

        return new OperationResponse(outcome, descriptor.EncodeResponse(response));
    }

    /// <summary>
    /// 把调用结果与上下文里的响应对象转成类型化结果。
    /// </summary>
    private static Result<TResponse> ToTypedResult<TResponse>(InvocationContext context, OperationDescriptor descriptor, Result outcome)
    {
        if (outcome.IsFailure)
        {
            return outcome.Error!;
        }

        return context.GetOperationResponse().TryGetValue(out var response) && response is TResponse typed
            ? Result.Success(typed)
            : Result.Failure<TResponse>(Error.Unexpected(KernelErrors.Unexpected, KernelMessages.OperationNoResponse, details: MessageArguments.Of(("id", descriptor.Id))));
    }

    /// <summary>
    /// 未登记操作的错误。
    /// </summary>
    private static Error Unknown(string operationId)
        => Error.NotFound(KernelErrors.UnknownOperation, KernelMessages.UnknownOperation, MessageArguments.Of(("id", operationId)));
}
