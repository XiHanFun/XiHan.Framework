// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Wasm.Tests.Fixtures;

/// <summary>
/// 什么都不做的宿主中间件
/// </summary>
/// <remarks>
/// 只用来在管道里占一个位置，核对插件中间件相对它的次序
/// </remarks>
internal sealed class MarkerMiddleware : IPipelineMiddleware
{
    /// <inheritdoc />
    public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(nextStep);
        return nextStep(context);
    }
}
