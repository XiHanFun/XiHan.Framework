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
/// 内置中间件与上下文扩展的行为
/// </summary>
/// <remarks>
/// <see cref="DeadlineMiddleware"/> 对超时与取消的翻译、<see cref="ObserveMiddleware"/> 对异常的收敛、强类型 Items 与结果槽
/// </remarks>
public sealed class MiddlewareTests
{
    /// <summary>
    /// 空容器
    /// </summary>
    /// <remarks>
    /// 这些测试直接调用中间件，不经过容器解析
    /// </remarks>
    private static readonly ServiceProvider _services = new ServiceCollection().BuildServiceProvider();

    [Fact]
    public async Task Deadline_AlreadyExpired_ShortCircuits()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero));
        var middleware = new DeadlineMiddleware(time);
        var context = App.CreateContext(_services, new InvocationContextOptions { Deadline = time.GetUtcNow().AddSeconds(-1) }, CancellationToken.None);
        var called = false;

        await middleware.InvokeAsync(context, _ =>
        {
            called = true;
            return ValueTask.CompletedTask;
        });

        Assert.False(called);
        Assert.Equal(KernelErrors.DeadlineExceeded, context.FindOutcome().ValueOrThrow().Error!.Code);
    }

    [Fact]
    public async Task Deadline_Reached_CancelsInnerAndRecordsTimeout()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero));
        var middleware = new DeadlineMiddleware(time);
        var context = App.CreateContext(_services, new InvocationContextOptions { Deadline = time.GetUtcNow().AddSeconds(5) }, CancellationToken.None);

        // 下游先挂起，再推进虚拟时间越过截止点，触发定时取消
        var invocation = middleware.InvokeAsync(context, async inner =>
        {
            Assert.NotEqual(context.CancellationToken, inner.CancellationToken);
            await Task.Delay(Timeout.InfiniteTimeSpan, inner.CancellationToken);
        });
        time.Advance(TimeSpan.FromSeconds(6));
        await invocation;

        Assert.Equal(KernelErrors.DeadlineExceeded, context.FindOutcome().ValueOrThrow().Error!.Code);
    }

    [Fact]
    public async Task Deadline_Reached_WhileDownstreamReportsCancellationAsAResult_RecordsTimeout()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero));
        var middleware = new DeadlineMiddleware(time);
        var context = App.CreateContext(_services, new InvocationContextOptions { Deadline = time.GetUtcNow().AddSeconds(5) }, CancellationToken.None);

        // 下游（如插件运行时）接住取消、自己写下取消结果、正常返回，不抛异常
        var invocation = middleware.InvokeAsync(context, async inner =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, inner.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                inner.SetOutcome(new Error(KernelErrors.Cancelled, KernelMessages.Cancelled));
            }
        });
        time.Advance(TimeSpan.FromSeconds(6));
        await invocation;

        // 截止令牌触发、外部令牌没有：按超时报告
        Assert.Equal(KernelErrors.DeadlineExceeded, context.FindOutcome().ValueOrThrow().Error!.Code);
    }

    [Fact]
    public async Task Deadline_ExternalCancellation_ReportedAsAResult_StaysCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        var time = new FakeTimeProvider();
        var middleware = new DeadlineMiddleware(time);
        var context = App.CreateContext(_services, new InvocationContextOptions { Deadline = time.GetUtcNow().AddMinutes(1) }, cancellation.Token);

        var invocation = middleware.InvokeAsync(context, async inner =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, inner.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                inner.SetOutcome(new Error(KernelErrors.Cancelled, KernelMessages.Cancelled));
            }
        });
        await cancellation.CancelAsync();
        await invocation;

        // 截止时间没到、外部取消：保持取消
        Assert.Equal(KernelErrors.Cancelled, context.FindOutcome().ValueOrThrow().Error!.Code);
    }

    [Fact]
    public async Task Deadline_ExternalCancellation_RecordsCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        var time = new FakeTimeProvider();
        var middleware = new DeadlineMiddleware(time);
        var context = App.CreateContext(_services, new InvocationContextOptions { Deadline = time.GetUtcNow().AddMinutes(1) }, cancellation.Token);

        var invocation = middleware.InvokeAsync(context, async inner => await Task.Delay(Timeout.InfiniteTimeSpan, inner.CancellationToken));
        await cancellation.CancelAsync();
        await invocation;

        Assert.Equal(KernelErrors.Cancelled, context.FindOutcome().ValueOrThrow().Error!.Code);
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
        Assert.False(context.FindOutcome().HasValue);
    }

    [Fact]
    public async Task Observe_UnhandledException_BecomesUnexpectedOutcomeWithInnerFrameworkError()
    {
        var middleware = new ObserveMiddleware(NullLoggerFactory.Instance);
        var context = App.CreateContext(_services, CancellationToken.None);

        await middleware.InvokeAsync(context, static _ => throw new FrameworkException("ORDERS_CONFLICT_001", "Conflict."));

        var error = context.FindOutcome().ValueOrThrow().Error!;
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

        Assert.True(context.FindOutcome().ValueOrThrow().IsSuccess);
    }

    [Fact]
    public void ContextItems_TypedKeysRoundTrip()
    {
        var key = new ContextItemKey<int>("test.number");
        var context = App.CreateContext(_services, CancellationToken.None);

        Assert.False(context.Find(key).HasValue);
        context.Set(key, 7);
        Assert.Equal(7, context.Find(key).ValueOrThrow());
        // 第二次 SetOutcomeIfMissing 不覆盖已有结果
        context.SetOutcomeIfMissing(Result.Success());
        context.SetOutcomeIfMissing(Error.Literal("ORDERS_CONFLICT_001", "Second outcome."));
        Assert.True(context.FindOutcome().ValueOrThrow().IsSuccess);
        Assert.Equal("test.number", key.ToString());
    }

    [Fact]
    public void Origin_IsAbsentUntilSet_AndSharedWithDerivedContexts()
    {
        var context = App.CreateContext(_services, CancellationToken.None);
        Assert.False(context.FindOrigin().HasValue);

        var origin = new InvocationOrigin { Protocol = InvocationProtocols.Http, Target = "GET /orders", RemoteAddress = "203.0.113.7" };
        context.SetOrigin(origin);
        Assert.Equal(origin, context.WithBaggage(BaggageKeys.Tenant, "acme").FindOrigin().ValueOrThrow());
        Assert.Equal(origin with { Protocol = InvocationProtocols.Grpc }, new InvocationOrigin { Protocol = "grpc", Target = "GET /orders", RemoteAddress = "203.0.113.7" });

        Assert.Throws<ArgumentException>(static () => new InvocationOrigin { Protocol = " ", Target = "x" });
        Assert.Throws<ArgumentNullException>(static () => new InvocationOrigin { Protocol = InvocationProtocols.Job, Target = null! });
    }
}
