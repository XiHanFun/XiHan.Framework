// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests;

internal sealed class TracingMiddleware(List<string> log, string name) : IPipelineMiddleware
{
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        log.Add($"enter:{name}");
        await nextStep(context);
        log.Add($"exit:{name}");
    }
}
