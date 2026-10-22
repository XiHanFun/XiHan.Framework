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
