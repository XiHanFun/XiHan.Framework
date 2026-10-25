// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 放在 Authorize 阶段的中间件：把上下文里的操作 ID 记到 <see cref="OperationCapture"/>。
/// </summary>
internal sealed class OperationCaptureMiddleware(OperationCapture capture) : IPipelineMiddleware
{
    /// <inheritdoc />
    public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        capture.Seen.Add(context.GetOperation().TryGetValue(out var operation) ? operation.Id : "-");
        return nextStep(context);
    }
}
