// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace SimpleApp.Greetings;

/// <summary>
/// 计数中间件：每次调用经过 Observe 阶段时把计数加一，然后放行。
/// </summary>
public sealed class CountingMiddleware(GreetingCounter counter) : IPipelineMiddleware
{
    /// <summary>
    /// 先计数再调用下一步。
    /// </summary>
    /// <remarks>
    /// 不改变调用结果。
    /// </remarks>
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        counter.Increment();
        await nextStep(context);
    }
}
