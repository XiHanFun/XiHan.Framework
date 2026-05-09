// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// 管道中间件接口。每个中间件在管道中处理上下文并决定是否调用下一个。
/// </summary>
[ApiLevel(Stability.Stable, "1.0")]
public interface IPipelineMiddleware
{
    /// <summary>
    /// 执行中间件逻辑。
    /// </summary>
    /// <param name="context">管道上下文。</param>
    /// <param name="nextHandler">下一个中间件委托。</param>
    Task InvokeAsync(PipelineContext context, PipelineDelegate nextHandler);
}
