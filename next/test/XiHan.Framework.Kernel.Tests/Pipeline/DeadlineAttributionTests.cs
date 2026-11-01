// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Pipeline;

/// <summary>
/// <see cref="DeadlineMiddleware"/> 对截止时间与调用方取消的归因。
/// </summary>
/// <remarks>
/// 下游先挂起、不看令牌，用例按需推进注入的时钟（触发截止令牌）与取消调用方的令牌之后才放行，下游再按令牌抛出取消或写下取消结果：
/// 下游观察时该触发的令牌都已触发，结果与触发顺序、线程调度无关。截止令牌已触发即超时，调用方同时取消也一样，两条路径同一规则（ADR-0092）。
/// </remarks>
public sealed class DeadlineAttributionTests
{
    /// <summary>
    /// 空容器。
    /// </summary>
    /// <remarks>
    /// 用例直接调用中间件，不经过容器解析。
    /// </remarks>
    private static readonly ServiceProvider _services = new ServiceCollection().BuildServiceProvider();

    /// <summary>
    /// 截止时间距调用开始的时长。
    /// </summary>
    private static readonly TimeSpan _deadlineAfter = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 下游观察到取消之前触发了哪些令牌。
    /// </summary>
    public enum Trigger
    {
        /// <summary>
        /// 只有截止时间到了。
        /// </summary>
        DeadlineOnly,

        /// <summary>
        /// 只有调用方取消。
        /// </summary>
        CallerOnly,

        /// <summary>
        /// 截止时间先到，调用方随后也取消。
        /// </summary>
        DeadlineThenCaller,

        /// <summary>
        /// 调用方先取消，截止时间随后也到。
        /// </summary>
        CallerThenDeadline,
    }

    /// <summary>
    /// 下游如何报告取消。
    /// </summary>
    public enum Downstream
    {
        /// <summary>
        /// 抛出 <see cref="OperationCanceledException"/>。
        /// </summary>
        Throws,

        /// <summary>
        /// 接住取消、写下 <see cref="KernelErrors.Cancelled"/> 结果、正常返回（插件运行时的写法）。
        /// </summary>
        WritesResult,
    }

    [Theory]
    [InlineData(Trigger.DeadlineOnly, Downstream.Throws, KernelErrors.DeadlineExceeded)]
    [InlineData(Trigger.DeadlineOnly, Downstream.WritesResult, KernelErrors.DeadlineExceeded)]
    [InlineData(Trigger.CallerOnly, Downstream.Throws, KernelErrors.Cancelled)]
    [InlineData(Trigger.CallerOnly, Downstream.WritesResult, KernelErrors.Cancelled)]
    [InlineData(Trigger.DeadlineThenCaller, Downstream.Throws, KernelErrors.DeadlineExceeded)]
    [InlineData(Trigger.DeadlineThenCaller, Downstream.WritesResult, KernelErrors.DeadlineExceeded)]
    [InlineData(Trigger.CallerThenDeadline, Downstream.Throws, KernelErrors.DeadlineExceeded)]
    [InlineData(Trigger.CallerThenDeadline, Downstream.WritesResult, KernelErrors.DeadlineExceeded)]
    public async Task Deadline_IsAttributedByTheDeadlineToken_OnBothPaths(Trigger trigger, Downstream downstream, string expected)
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
        using var caller = new CancellationTokenSource();
        var middleware = new DeadlineMiddleware(time);
        var context = App.CreateContext(_services, caller.Token, deadline: time.GetUtcNow() + _deadlineAfter);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var invocation = middleware.InvokeAsync(context, async inner =>
        {
            await release.Task;
            if (downstream == Downstream.Throws)
            {
                inner.CancellationToken.ThrowIfCancellationRequested();
            }
            else if (inner.CancellationToken.IsCancellationRequested)
            {
                inner.SetOutcome(new Error(KernelErrors.Cancelled, KernelMessages.Cancelled));
            }
        });

        // 推进时钟越过截止时间，定时取消在 Advance 里同步触发；调用方的令牌按场景先后取消
        if (trigger is Trigger.CallerOnly or Trigger.CallerThenDeadline)
        {
            await caller.CancelAsync();
        }

        if (trigger is not Trigger.CallerOnly)
        {
            time.Advance(_deadlineAfter);
        }

        if (trigger is Trigger.DeadlineThenCaller)
        {
            await caller.CancelAsync();
        }

        release.SetResult();
        await invocation;

        Assert.Equal(expected, context.GetOutcome().ValueOrThrow().Error!.Code);
    }
}
