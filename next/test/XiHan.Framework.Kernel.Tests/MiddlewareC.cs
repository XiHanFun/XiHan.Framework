// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests;

internal sealed class MiddlewareC(List<string> log) : IPipelineMiddleware
{
    private readonly TracingMiddleware _inner = new(log, "C");

    public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep) => _inner.InvokeAsync(context, nextStep);
}
