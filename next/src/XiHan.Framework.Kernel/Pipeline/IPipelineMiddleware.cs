// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// 管道中的一步：一个中间件之后的剩余管道，或终点处理器。
/// </summary>
/// <param name="context">当前调用的上下文。</param>
[ApiLevel(Stability.Stable, "5.0")]
public delegate ValueTask PipelineStep(InvocationContext context);

/// <summary>
/// 管道中处理一次调用的中间件。中间件从调用作用域的服务提供器解析，可以是单例或作用域生命周期。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IPipelineMiddleware
{
    /// <summary>
    /// 执行中间件逻辑并按需调用管道中的下一步。
    /// </summary>
    /// <param name="context">当前调用的上下文。</param>
    /// <param name="nextStep">管道中的下一步。</param>
    ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep);
}

/// <summary>
/// 中间件在所属阶段内的相对位置。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public enum Placement
{
    /// <summary>阶段末尾（默认）。</summary>
    End = 0,

    /// <summary>阶段开头。</summary>
    Beginning = 1,
}
