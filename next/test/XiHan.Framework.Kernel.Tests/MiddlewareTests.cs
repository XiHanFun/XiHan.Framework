// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests;

public sealed class MiddlewareTests
{
    private static readonly ServiceProvider Services = new ServiceCollection().BuildServiceProvider();

    [Fact]
    public async Task Deadline_AlreadyExpired_ShortCircuits()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero));
        var middleware = new DeadlineMiddleware(time);
        var context = App.CreateContext(Services, CancellationToken.None, deadline: time.GetUtcNow().AddSeconds(-1));
        var called = false;

        await middleware.InvokeAsync(context, _ =>
        {
            called = true;
            return ValueTask.CompletedTask;
        });

        Assert.False(called);
        Assert.Equal(KernelErrors.DeadlineExceeded, context.GetOutcome().ValueOrThrow().Error!.Code);
    }

    [Fact]
    public async Task Deadline_Reached_CancelsInnerAndRecordsTimeout()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero));
        var middleware = new DeadlineMiddleware(time);
        var context = App.CreateContext(Services, CancellationToken.None, deadline: time.GetUtcNow().AddSeconds(5));

        var invocation = middleware.InvokeAsync(context, async inner =>
        {
            Assert.NotEqual(context.CancellationToken, inner.CancellationToken);
            await Task.Delay(Timeout.InfiniteTimeSpan, inner.CancellationToken);
        });
        time.Advance(TimeSpan.FromSeconds(6));
        await invocation;

        Assert.Equal(KernelErrors.DeadlineExceeded, context.GetOutcome().ValueOrThrow().Error!.Code);
    }

    [Fact]
    public async Task Deadline_ExternalCancellation_RecordsCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        var time = new FakeTimeProvider();
        var middleware = new DeadlineMiddleware(time);
        var context = App.CreateContext(Services, cancellation.Token, deadline: time.GetUtcNow().AddMinutes(1));

        var invocation = middleware.InvokeAsync(context, async inner => await Task.Delay(Timeout.InfiniteTimeSpan, inner.CancellationToken));
        await cancellation.CancelAsync();
        await invocation;

        Assert.Equal(KernelErrors.Cancelled, context.GetOutcome().ValueOrThrow().Error!.Code);
    }

    [Fact]
    public async Task Deadline_NoDeadline_PassesOriginalContext()
    {
        var middleware = new DeadlineMiddleware(new FakeTimeProvider());
        var context = App.CreateContext(Services, CancellationToken.None);
        InvocationContext? seen = null;

        await middleware.InvokeAsync(context, inner =>
        {
            seen = inner;
            return ValueTask.CompletedTask;
        });

        Assert.Same(context, seen);
        Assert.False(context.GetOutcome().HasValue);
    }

    [Fact]
    public async Task Observe_UnhandledException_BecomesUnexpectedOutcomeWithInnerFrameworkError()
    {
        var middleware = new ObserveMiddleware(NullLoggerFactory.Instance);
        var context = App.CreateContext(Services, CancellationToken.None);

        await middleware.InvokeAsync(context, static _ => throw new FrameworkException("ORDERS_CONFLICT_001", "冲突"));

        var error = context.GetOutcome().ValueOrThrow().Error!;
        Assert.Equal(KernelErrors.Unexpected, error.Code);
        Assert.Equal("ORDERS_CONFLICT_001", error.Inner!.Code);
    }

    [Fact]
    public async Task Observe_Success_LeavesOutcomeUntouched()
    {
        var middleware = new ObserveMiddleware(NullLoggerFactory.Instance);
        var context = App.CreateContext(Services, CancellationToken.None);

        await middleware.InvokeAsync(context, static inner =>
        {
            inner.SetOutcome(Result.Success());
            return ValueTask.CompletedTask;
        });

        Assert.True(context.GetOutcome().ValueOrThrow().IsSuccess);
    }

    [Fact]
    public void ContextItems_TypedKeysRoundTrip()
    {
        var key = new ContextItemKey<int>("test.number");
        var context = App.CreateContext(Services, CancellationToken.None);

        Assert.False(context.Get(key).HasValue);
        context.Set(key, 7);
        Assert.Equal(7, context.Get(key).ValueOrThrow());
        context.SetOutcomeIfMissing(Result.Success());
        context.SetOutcomeIfMissing(Error.Conflict("ORDERS_CONFLICT_001", "后来者"));
        Assert.True(context.GetOutcome().ValueOrThrow().IsSuccess);
        Assert.Equal("test.number", key.ToString());
    }
}
