// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace SimpleApp;

public sealed class CountingMiddleware(GreetingCounter counter) : IPipelineMiddleware
{
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        counter.Increment();
        await nextStep(context);
    }
}
