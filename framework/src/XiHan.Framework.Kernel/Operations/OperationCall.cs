// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// XGI 入口经完整管道的一次调用
/// </summary>
/// <remarks>
/// 交给 <see cref="App.InvokeAsync"/> 的处理器就是它的 <see cref="HandleAsync"/>。Observe 阶段的内置中间件进入时按处理器认出它，在排在后面的中间件都结束之后、
/// 定结果与结束 Span 之前调 <see cref="Encode"/>：写不出时 Span 与指标照调用方拿到的 <see cref="KernelErrors.Unexpected"/> 记失败（RFC-0023「内核的遥测」，ADR-0341）。
/// 没有这个中间件（关掉了内置中间件）时在管道之后编码。它代替以前捕获描述与请求的闭包，每次调用照旧是一个对象加一个委托
/// </remarks>
/// <param name="invoker">发起调用的调用器</param>
/// <param name="descriptor">被调用的操作</param>
/// <param name="request">XGI 请求</param>
internal sealed class OperationCall(OperationInvoker invoker, OperationDescriptor descriptor, OperationRequest request)
{
    /// <summary>
    /// 在管道里编出的响应
    /// </summary>
    /// <remarks>
    /// 管道里以成功结束、Observe 阶段编过码时才有
    /// </remarks>
    private OperationResponse? _encoded;

    /// <summary>
    /// 找出这次管道调用的 XGI 调用
    /// </summary>
    /// <remarks>
    /// 处理器不是 <see cref="HandleAsync"/>（消息、作业、宿主端点，或直接交给 <see cref="App.InvokeAsync"/> 的处理器）时为 <see langword="null"/>。
    /// 要在进入管道时取：处理器在同一个上下文上再发起的调用会换掉上下文里的处理器
    /// </remarks>
    /// <param name="context">调用上下文</param>
    public static OperationCall? Find(InvocationContext context) => App.FindHandler(context)?.Target as OperationCall;

    /// <summary>
    /// Handle 阶段的终点
    /// </summary>
    /// <param name="context">调用上下文</param>
    public ValueTask HandleAsync(InvocationContext context) => invoker.DispatchAsync(context, descriptor, request.Payload);

    /// <summary>
    /// 在管道里编码响应
    /// </summary>
    /// <remarks>
    /// 管道里的结果是失败时不编码；写不出时把结果换成 <see cref="KernelErrors.Unexpected"/>，Observe 阶段随后据此定结果
    /// </remarks>
    /// <param name="context">调用上下文</param>
    public void Encode(InvocationContext context)
    {
        var outcome = context.FindOutcome().ValueOr(Result.Success());
        if (outcome.IsSuccess)
        {
            _encoded = invoker.ToResponse(context, descriptor, outcome);
        }
    }

    /// <summary>
    /// 管道结束之后交出 XGI 响应
    /// </summary>
    /// <remarks>
    /// 管道交出的结果是成功且已经编过码时交出编出的；否则照管道交出的结果组装，还没编码的（没有 Observe 阶段的内置中间件）在这里编码
    /// </remarks>
    /// <param name="context">调用上下文</param>
    /// <param name="outcome">管道交出的结果</param>
    public OperationResponse Complete(InvocationContext context, Result outcome)
        => outcome.IsSuccess && _encoded is { } encoded ? encoded : invoker.ToResponse(context, descriptor, outcome);
}
