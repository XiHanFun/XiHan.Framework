// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// 调用上下文里的操作信息。
/// </summary>
/// <remarks>
/// 当前调用的操作描述（供 Authorize、Validate 等阶段与协议适配器读取）与执行后的响应对象。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class OperationInvocationExtensions
{
    /// <summary>
    /// 当前调用的操作描述。
    /// </summary>
    private static readonly ContextItemKey<OperationDescriptor> _operationKey = new("xihan.operation");

    /// <summary>
    /// 当前调用的原始载荷（UTF-8 JSON）。
    /// </summary>
    private static readonly ContextItemKey<ReadOnlyMemory<byte>> _payloadKey = new("xihan.operation.payload");

    /// <summary>
    /// 当前调用的请求对象（解码后）。
    /// </summary>
    private static readonly ContextItemKey<object> _requestKey = new("xihan.operation.request");

    /// <summary>
    /// 执行成功后的响应对象。
    /// </summary>
    private static readonly ContextItemKey<object> _responseKey = new("xihan.operation.response");

    /// <summary>
    /// 把本次调用要执行的操作放进上下文。
    /// </summary>
    /// <remarks>
    /// 调用器与宿主在管道开始前设置。
    /// </remarks>
    public static void SetOperation(this InvocationContext context, OperationDescriptor descriptor) => context.Set(_operationKey, descriptor);

    /// <summary>
    /// 取本次调用的操作描述。
    /// </summary>
    /// <remarks>
    /// 不是操作调用时无值。
    /// </remarks>
    public static Maybe<OperationDescriptor> GetOperation(this InvocationContext context) => context.Get(_operationKey);

    /// <summary>
    /// 把本次调用的原始载荷（UTF-8 JSON，空表示 <c>{}</c>）放进上下文。
    /// </summary>
    /// <remarks>
    /// 调用器与宿主在管道开始前设置，Validate 阶段据此校验 Schema 并解码。
    /// </remarks>
    public static void SetOperationPayload(this InvocationContext context, ReadOnlyMemory<byte> payload) => context.Set(_payloadKey, payload);

    /// <summary>
    /// 取本次调用的原始载荷。
    /// </summary>
    /// <remarks>
    /// 类型化调用没有载荷时无值。
    /// </remarks>
    public static Maybe<ReadOnlyMemory<byte>> GetOperationPayload(this InvocationContext context) => context.Get(_payloadKey);

    /// <summary>
    /// 记录解码后的请求对象，供 Validate 等后续阶段检查。
    /// </summary>
    public static void SetOperationRequest(this InvocationContext context, object request) => context.Set(_requestKey, request);

    /// <summary>
    /// 取解码后的请求对象。
    /// </summary>
    public static Maybe<object> GetOperationRequest(this InvocationContext context) => context.Get(_requestKey);

    /// <summary>
    /// 记录执行成功的响应对象。
    /// </summary>
    public static void SetOperationResponse(this InvocationContext context, object response) => context.Set(_responseKey, response);

    /// <summary>
    /// 取执行成功的响应对象。
    /// </summary>
    public static Maybe<object> GetOperationResponse(this InvocationContext context) => context.Get(_responseKey);
}
