// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests;

/// <summary>
/// 什么都不做、无依赖的中间件 D，用于只关心排序与注册而不关心执行的测试。
/// </summary>
internal sealed class MiddlewareD : IPipelineMiddleware
{
    /// <summary>
    /// 直接调用下一步。
    /// </summary>
    public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep) => nextStep(context);
}
