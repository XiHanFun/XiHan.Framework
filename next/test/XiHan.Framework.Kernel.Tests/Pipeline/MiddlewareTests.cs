// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Pipeline;

/// <summary>
/// 内置中间件与上下文扩展的行为。
/// </summary>
/// <remarks>
/// <see cref="DeadlineMiddleware"/> 对超时与取消的翻译、<see cref="ObserveMiddleware"/> 对异常的收敛、强类型 Items 与结果槽。
/// </remarks>
public sealed class MiddlewareTests
{
    /// <summary>
    /// 空容器。
    /// </summary>
    /// <remarks>
    /// 这些测试直接调用中间件，不经过容器解析。
    /// </remarks>
    private static readonly ServiceProvider _services = new ServiceCollection().BuildServiceProvider();

    [Fact]
    public async Task Deadline_AlreadyExpired_ShortCircuits()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero));
        var middleware = new DeadlineMiddleware(time);
        var context = App.CreateContext(_services, CancellationToken.None, deadline: time.GetUtcNow().AddSeconds(-1));
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
        var context = App.CreateContext(_services, CancellationToken.None, deadline: time.GetUtcNow().AddSeconds(5));

        // 下游先挂起，再推进虚拟时间越过截止点，触发定时取消。
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
        var context = App.CreateContext(_services, cancellation.Token, deadline: time.GetUtcNow().AddMinutes(1));

        var invocation = middleware.InvokeAsync(context, async inner => await Task.Delay(Timeout.InfiniteTimeSpan, inner.CancellationToken));
        await cancellation.CancelAsync();
        await invocation;

        Assert.Equal(KernelErrors.Cancelled, context.GetOutcome().ValueOrThrow().Error!.Code);
    }

    [Fact]
    public async Task Deadline_NoDeadline_PassesOriginalContext()
    {
        var middleware = new DeadlineMiddleware(new FakeTimeProvider());
        var context = App.CreateContext(_services, CancellationToken.None);
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
        var context = App.CreateContext(_services, CancellationToken.None);

        await middleware.InvokeAsync(context, static _ => throw new FrameworkException("ORDERS_CONFLICT_001", "Conflict."));

        var error = context.GetOutcome().ValueOrThrow().Error!;
        Assert.Equal(KernelErrors.Unexpected, error.Code);
        Assert.Equal("ORDERS_CONFLICT_001", error.Inner!.Code);
    }

    [Fact]
    public async Task Observe_Success_LeavesOutcomeUntouched()
    {
        var middleware = new ObserveMiddleware(NullLoggerFactory.Instance);
        var context = App.CreateContext(_services, CancellationToken.None);

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
        var context = App.CreateContext(_services, CancellationToken.None);

        Assert.False(context.Get(key).HasValue);
        context.Set(key, 7);
        Assert.Equal(7, context.Get(key).ValueOrThrow());
        // 第二次 SetOutcomeIfMissing 不覆盖已有结果。
        context.SetOutcomeIfMissing(Result.Success());
        context.SetOutcomeIfMissing(Error.Literal("ORDERS_CONFLICT_001", "Second outcome."));
        Assert.True(context.GetOutcome().ValueOrThrow().IsSuccess);
        Assert.Equal("test.number", key.ToString());
    }

    [Fact]
    public void Origin_IsAbsentUntilSet_AndSharedWithDerivedContexts()
    {
        var context = App.CreateContext(_services, CancellationToken.None);
        Assert.False(context.GetOrigin().HasValue);

        var origin = new InvocationOrigin { Protocol = InvocationProtocols.Http, Target = "GET /orders", RemoteAddress = "203.0.113.7" };
        context.SetOrigin(origin);
        Assert.Equal(origin, context.WithBaggage(BaggageKeys.Tenant, "acme").GetOrigin().ValueOrThrow());
        Assert.Equal(origin with { Protocol = InvocationProtocols.Grpc }, new InvocationOrigin { Protocol = "grpc", Target = "GET /orders", RemoteAddress = "203.0.113.7" });

        Assert.Throws<ArgumentException>(static () => new InvocationOrigin { Protocol = " ", Target = "x" });
        Assert.Throws<ArgumentNullException>(static () => new InvocationOrigin { Protocol = InvocationProtocols.Job, Target = null! });
    }
}
