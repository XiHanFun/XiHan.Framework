// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests;

/// <summary>
/// 记录进出事件的中间件 A。管道中每个类型只能出现一次，因此用独立类型而不是同一类型的多个实例。
/// </summary>
/// <param name="log">事件日志。</param>
internal sealed class MiddlewareA(List<string> log) : IPipelineMiddleware
{
    /// <summary>
    /// 实际记录事件的实现，名称为 <c>A</c>。
    /// </summary>
    private readonly TracingMiddleware _inner = new(log, "A");

    /// <summary>
    /// 委托给 <see cref="_inner"/>。
    /// </summary>
    public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep) => _inner.InvokeAsync(context, nextStep);
}
