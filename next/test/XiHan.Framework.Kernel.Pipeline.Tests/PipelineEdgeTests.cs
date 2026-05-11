// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Xunit;

namespace XiHan.Framework.Kernel.Pipeline.Tests;

public class PipelineEdgeTests
{
    [Fact]
    public void UseBefore_TargetNotPresent_ShouldThrow()
    {
        var builder = new PipelineBuilder();
        Assert.Throws<InvalidOperationException>(() =>
            builder.UseBefore<EdgeEchoMiddleware, EdgeCounterMiddleware>());
    }

    [Fact]
    public void UseAfter_TargetNotPresent_ShouldThrow()
    {
        var builder = new PipelineBuilder();
        Assert.Throws<InvalidOperationException>(() =>
            builder.UseAfter<EdgeEchoMiddleware, EdgeCounterMiddleware>());
    }

    [Fact]
    public async Task UseBefore_WithTargetInMiddle_ShouldInsertCorrectly()
    {
        var builder = new PipelineBuilder();
        builder.Use<EdgeCounterMiddleware>();
        builder.Use<EdgeEchoMiddleware>();
        builder.UseBefore<EdgeCounterMiddleware, EdgeEchoMiddleware>();

        var context = new PipelineContext();
        await builder.Build()(context);

        Assert.Equal(2, context.Get("edge-count"));
    }

    [Fact]
    public async Task UseAfter_WithTarget_ShouldInsertAfter()
    {
        var builder = new PipelineBuilder();
        builder.Use<EdgeCounterMiddleware>();
        builder.Use<EdgeEchoMiddleware>();
        builder.UseAfter<EdgeCounterMiddleware, EdgeEchoMiddleware>();

        var context = new PipelineContext();
        await builder.Build()(context);

        Assert.Equal(2, context.Get("edge-count"));
    }
}

internal sealed class EdgeCounterMiddleware : IPipelineMiddleware
{
    public Task InvokeAsync(PipelineContext context, PipelineHandler next)
    {
        context.Set("edge-count", (context.Get("edge-count") as int? ?? 0) + 1);
        return next(context);
    }
}

internal sealed class EdgeEchoMiddleware : IPipelineMiddleware
{
    public Task InvokeAsync(PipelineContext context, PipelineHandler next)
        => next(context);
}
