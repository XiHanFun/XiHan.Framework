// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 在调用下一步前后分别记录 <c>enter:</c> 与 <c>exit:</c> 事件的中间件，用于断言执行顺序。
/// </summary>
/// <param name="log">事件日志。</param>
/// <param name="name">事件里标识本中间件的名称。</param>
internal sealed class TracingMiddleware(List<string> log, string name) : IPipelineMiddleware
{
    /// <summary>
    /// 记录进入、调用下一步、记录退出。
    /// </summary>
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        log.Add($"enter:{name}");
        await nextStep(context);
        log.Add($"exit:{name}");
    }
}
