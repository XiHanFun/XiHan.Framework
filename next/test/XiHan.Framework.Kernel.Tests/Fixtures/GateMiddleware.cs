// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 挂在 Handle 阶段开头的可编程中间件：测试用它让下游阻塞、失败或抛异常，并数经过的调用。
/// </summary>
internal sealed class GateMiddleware : IPipelineMiddleware
{
    /// <summary>
    /// 经过的调用数。
    /// </summary>
    private int _calls;

    /// <summary>
    /// 每次调用的行为；默认直接放行。
    /// </summary>
    public Func<InvocationContext, PipelineStep, ValueTask> Behavior { get; set; } = static (context, next) => next(context);

    /// <summary>
    /// 经过的调用数。
    /// </summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <inheritdoc />
    public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        Interlocked.Increment(ref _calls);
        return Behavior(context, nextStep);
    }
}
