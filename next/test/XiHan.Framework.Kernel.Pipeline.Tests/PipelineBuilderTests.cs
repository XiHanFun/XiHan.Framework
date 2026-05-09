// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using Xunit;

namespace XiHan.Framework.Kernel.Pipeline.Tests;

public class PipelineBuilderTests
{
    [Fact]
    public async Task Build_EmptyPipeline_ShouldReturnCompletedTask()
    {
        var pipeline = new PipelineBuilder().Build();
        await pipeline(new PipelineContext());
    }

    [Fact]
    public async Task SingleMiddleware_ShouldExecute()
    {
        var builder = new PipelineBuilder();
        builder.Use<CountingMiddleware>();

        var context = new PipelineContext();
        await builder.Build()(context);

        Assert.Equal(1, context.Get("count"));
    }

    [Fact]
    public async Task MultipleMiddleware_ShouldExecuteInOrder()
    {
        OrderMiddleware.ResetCounter();
        var builder = new PipelineBuilder();
        builder.Use<OrderMiddleware>();
        builder.Use<OrderMiddleware>();
        builder.Use<OrderMiddleware>();

        var context = new PipelineContext();
        context.Set("order", new List<int>());
        await builder.Build()(context);

        Assert.Equal([1, 2, 3], context.Get("order") as List<int>);
    }

    [Fact]
    public async Task Middleware_CanShortCircuit()
    {
        var builder = new PipelineBuilder();
        builder.Use<ShortCircuitMiddleware>();
        builder.Use<CountingMiddleware>();

        var context = new PipelineContext();
        await builder.Build()(context);

        Assert.Null(context.Get("count"));
    }

    [Fact]
    public async Task PipelineContext_CarriesUserIdAndTraceId()
    {
        var traceId = Guid.NewGuid().ToString("N");
        var context = new PipelineContext { UserId = "user-1", TraceId = traceId };
        var builder = new PipelineBuilder();
        builder.Use<ContextCapturingMiddleware>();

        await builder.Build()(context);

        Assert.Equal("user-1", context.Get("captured.userId"));
        Assert.Equal(traceId, context.Get("captured.traceId"));
    }

    [Fact]
    public void UseAt_Beginning_ShouldInsertFirst()
    {
        OrderMiddleware.ResetCounter();
        var builder = new PipelineBuilder();
        builder.Use<OrderMiddleware>();
        builder.UseAt<OrderMiddleware>(PipelinePosition.Beginning);

        var context = new PipelineContext();
        context.Set("order", new List<int>());
        builder.Build()(context).GetAwaiter().GetResult();

        Assert.Equal([1, 2], context.Get("order") as List<int>);
    }

    [Fact]
    public void Use_InvalidType_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new PipelineBuilder().Use(typeof(string)));
    }

    [Fact]
    public void UseAt_Beginning_VersusRegular_ShouldExecBeginFirst()
    {
        OrderMiddleware.ResetCounter();
        var builder = new PipelineBuilder();
        // Add "end" middleware first, then insert "beginning" middleware
        builder.Use<OrderMiddleware>();       // should be position 1 (end)
        builder.UseAt<OrderMiddleware>(PipelinePosition.Beginning); // should be position 0 (beginning)

        var context = new PipelineContext();
        context.Set("order", new List<int>());
        builder.Build()(context).GetAwaiter().GetResult();

        var order = context.Get("order") as List<int>;
        // The one inserted at beginning should execute first, getting counter=1
        Assert.Equal(1, order![0]);
        Assert.Equal(2, order[1]);
    }
}

internal sealed class CountingMiddleware : IPipelineMiddleware
{
    public Task InvokeAsync(PipelineContext context, PipelineDelegate next)
    {
        context.Set("count", (context.Get("count") as int? ?? 0) + 1);
        return next(context);
    }
}

internal sealed class OrderMiddleware : IPipelineMiddleware
{
    private static int _counter;

    public static void ResetCounter() => _counter = 0;

    public Task InvokeAsync(PipelineContext context, PipelineDelegate next)
    {
        var order = context.Get("order") as List<int> ?? [];
        order.Add(Interlocked.Increment(ref _counter));
        context.Set("order", order);
        return next(context);
    }
}

internal sealed class ShortCircuitMiddleware : IPipelineMiddleware
{
    public Task InvokeAsync(PipelineContext context, PipelineDelegate next)
        => Task.CompletedTask;
}

internal sealed class ContextCapturingMiddleware : IPipelineMiddleware
{
    public Task InvokeAsync(PipelineContext context, PipelineDelegate next)
    {
        context.Set("captured.userId", context.UserId);
        context.Set("captured.traceId", context.TraceId);
        return next(context);
    }
}
