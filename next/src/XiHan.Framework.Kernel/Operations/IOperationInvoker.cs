// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// XGI 调用器。
/// </summary>
/// <remarks>
/// 协议适配器与进程内调用方按操作 ID 调用操作。<c>Invoke*</c> 让一次调用经过完整管道（操作描述在 Ingress 之前进入上下文，Authorize 阶段据此判定）；<c>Execute*</c> 在已经处于管道内的调用里直接执行（如宿主端点在 Handle 阶段调用）。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public interface IOperationInvoker
{
    /// <summary>
    /// 经完整管道调用。
    /// </summary>
    /// <remarks>
    /// 解码载荷、执行、编码响应。未登记的操作返回 <see cref="KernelErrors.UnknownOperation"/>，不进入管道。
    /// </remarks>
    ValueTask<OperationResponse> InvokeAsync(InvocationContext context, OperationRequest request);

    /// <summary>
    /// 在当前调用内执行（调用方已在管道里）。
    /// </summary>
    /// <remarks>
    /// 解码载荷、执行、编码响应，并把结果写入上下文。
    /// </remarks>
    ValueTask<OperationResponse> ExecuteAsync(InvocationContext context, OperationRequest request);

    /// <summary>
    /// 经完整管道做类型化调用。
    /// </summary>
    /// <remarks>
    /// 操作的请求或响应类型与类型参数不符返回 <see cref="KernelErrors.OperationTypeMismatch"/>。
    /// </remarks>
    ValueTask<Result<TResponse>> InvokeAsync<TRequest, TResponse>(InvocationContext context, string operationId, TRequest request);

    /// <summary>
    /// 在当前调用内做类型化执行。
    /// </summary>
    ValueTask<Result<TResponse>> ExecuteAsync<TRequest, TResponse>(InvocationContext context, string operationId, TRequest request);
}
