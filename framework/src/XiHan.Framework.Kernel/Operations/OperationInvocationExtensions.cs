// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// 调用上下文里的操作信息
/// </summary>
/// <remarks>
/// 当前调用的操作描述（供 Authorize、Validate 等阶段与协议适配器读取）与执行后的响应对象
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class OperationInvocationExtensions
{
    /// <summary>
    /// 当前调用的操作描述
    /// </summary>
    private static readonly ContextItemKey<OperationDescriptor> _operationKey = new("xihan.operation");

    /// <summary>
    /// 当前调用的原始载荷（UTF-8 JSON）
    /// </summary>
    private static readonly ContextItemKey<ReadOnlyMemory<byte>> _payloadKey = new("xihan.operation.payload");

    /// <summary>
    /// 当前调用延迟读取的载荷来源
    /// </summary>
    private static readonly ContextItemKey<OperationPayloadSource> _payloadSourceKey = new("xihan.operation.payload-source");

    /// <summary>
    /// 当前调用的请求对象（解码后）
    /// </summary>
    private static readonly ContextItemKey<object> _requestKey = new("xihan.operation.request");

    /// <summary>
    /// 执行成功后的响应对象
    /// </summary>
    private static readonly ContextItemKey<object> _responseKey = new("xihan.operation.response");

    /// <summary>
    /// 把本次调用要执行的操作放进上下文
    /// </summary>
    /// <remarks>
    /// 调用器与宿主在管道开始前设置
    /// </remarks>
    public static void SetOperation(this InvocationContext context, OperationDescriptor descriptor) => context.Set(_operationKey, descriptor);

    /// <summary>
    /// 取本次调用的操作描述
    /// </summary>
    /// <remarks>
    /// 不是操作调用时无值
    /// </remarks>
    public static Maybe<OperationDescriptor> FindOperation(this InvocationContext context) => context.Find(_operationKey);

    /// <summary>
    /// 把本次调用的原始载荷（UTF-8 JSON，空表示 <c>{}</c>）放进上下文
    /// </summary>
    /// <remarks>
    /// 调用器与宿主在管道开始前设置，Validate 阶段据此校验 Schema 并解码
    /// </remarks>
    public static void SetOperationPayload(this InvocationContext context, ReadOnlyMemory<byte> payload) => context.Set(_payloadKey, payload);

    /// <summary>
    /// 取本次调用的原始载荷
    /// </summary>
    /// <remarks>
    /// 类型化调用没有载荷时无值
    /// </remarks>
    public static Maybe<ReadOnlyMemory<byte>> FindOperationPayload(this InvocationContext context) => context.Find(_payloadKey);

    /// <summary>
    /// 放入延迟读取的载荷来源
    /// </summary>
    /// <remarks>
    /// 宿主在管道开始前设置，代替进管道前就读请求体：Validate 阶段（Authorize 之后）才读，没有认证、没有权限或被限流的调用不会让宿主缓冲请求体（ADR-0176）
    /// </remarks>
    public static void SetOperationPayloadSource(this InvocationContext context, OperationPayloadSource source)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);
        context.Set(_payloadSourceKey, source);
    }

    /// <summary>
    /// 解析本次调用的原始载荷
    /// </summary>
    /// <remarks>
    /// 已有载荷直接返回；没有而有载荷来源时读一次并放进上下文（之后再调用直接返回它）；读取失败返回该失败；两者都没有时无值。Validate 阶段先调用它；关掉内置中间件、没有 Validate 阶段时，由执行操作的一方调用
    /// </remarks>
    public static async ValueTask<Result<Maybe<ReadOnlyMemory<byte>>>> ResolveOperationPayloadAsync(this InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Find(_payloadKey).TryGetValue(out var present))
        {
            return Maybe.Some(present);
        }

        if (!context.Find(_payloadSourceKey).TryGetValue(out var source))
        {
            return Maybe.None<ReadOnlyMemory<byte>>();
        }

        var read = await source(context.CancellationToken).ConfigureAwait(false);
        if (!read.TryGetValue(out var payload))
        {
            return read.Error!;
        }

        context.Set(_payloadKey, payload);
        return Maybe.Some(payload);
    }

    /// <summary>
    /// 记录解码后的请求对象，供 Validate 等后续阶段检查
    /// </summary>
    public static void SetOperationRequest(this InvocationContext context, object request) => context.Set(_requestKey, request);

    /// <summary>
    /// 取解码后的请求对象
    /// </summary>
    public static Maybe<object> FindOperationRequest(this InvocationContext context) => context.Find(_requestKey);

    /// <summary>
    /// 记录执行成功的响应对象
    /// </summary>
    public static void SetOperationResponse(this InvocationContext context, object response) => context.Set(_responseKey, response);

    /// <summary>
    /// 取执行成功的响应对象
    /// </summary>
    public static Maybe<object> FindOperationResponse(this InvocationContext context) => context.Find(_responseKey);
}
