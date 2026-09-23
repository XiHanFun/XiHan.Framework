// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Threading;

namespace XiHan.Framework.Utils.Tests.Threading;

/// <summary>
/// 防抖器测试
/// </summary>
/// <remarks>
/// 除防抖本身的语义外，锁两处修复：
/// 每次触发都新建 CancellationTokenSource 却从不释放旧的，调用多少次就泄漏多少个；
/// 释放之后再触发会从内部的令牌源抛出 ObjectDisposedException，
/// 而防抖器的典型宿主是文件监听、输入变更这类异步回调，
/// 调用方刚判断完未释放、释放就可能在下一行发生，异常会被抛进别人的回调栈。
/// <para>
/// 等待一律用信号加宽松上限，不用固定时长睡眠：
/// 本测试程序集里有农历全区间遍历、日志压测等 CPU 密集用例并行执行，
/// 线程池被占满时延时任务的续体会被推迟很久，按固定时长断言必然随机红。
/// 触发动作本身是同步调用，不受线程池状态影响，因此"一批触发落在同一间隔内"仍然成立。
/// </para>
/// </remarks>
public class DebouncerTests
{
    /// <summary>
    /// 静默间隔，需大于一批同步触发的耗时
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// 等待动作执行的上限，取得足够宽松以容忍线程池饥饿
    /// </summary>
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 间隔内的连续触发只执行最后一次
    /// </summary>
    [Fact]
    public async Task Debounce_WhenCalledRepeatedly_RunsOnlyLastAction()
    {
        using var debouncer = new Debouncer(Interval);
        var executions = 0;
        var last = 0;
        var signal = CreateSignal();

        for (var i = 1; i <= 5; i++)
        {
            var current = i;
            debouncer.Debounce(() =>
            {
                Interlocked.Increment(ref executions);
                Volatile.Write(ref last, current);
                signal.TrySetResult();
            });
        }

        await signal.Task.WaitAsync(WaitLimit);

        // 其余四次已被确定性取消，执行次数不会再增长
        Assert.Equal(1, Volatile.Read(ref executions));
        Assert.Equal(5, Volatile.Read(ref last));
    }

    /// <summary>
    /// 高频触发下依然只有最后一次生效
    /// </summary>
    [Fact]
    public async Task Debounce_UnderHighFrequency_RunsOnlyLastAction()
    {
        using var debouncer = new Debouncer(Interval);
        var executions = 0;
        var last = 0;
        var signal = CreateSignal();

        for (var i = 1; i <= 2000; i++)
        {
            var current = i;
            debouncer.Debounce(() =>
            {
                Interlocked.Increment(ref executions);
                Volatile.Write(ref last, current);
                signal.TrySetResult();
            });
        }

        await signal.Task.WaitAsync(WaitLimit);

        Assert.Equal(1, Volatile.Read(ref executions));
        Assert.Equal(2000, Volatile.Read(ref last));
    }

    /// <summary>
    /// 相隔足够久的触发各自执行
    /// </summary>
    [Fact]
    public async Task Debounce_WhenCallsAreFarApart_RunsEachTime()
    {
        using var debouncer = new Debouncer(Interval);
        var executions = 0;

        var first = CreateSignal();
        debouncer.Debounce(() =>
        {
            Interlocked.Increment(ref executions);
            first.TrySetResult();
        });
        await first.Task.WaitAsync(WaitLimit);

        var second = CreateSignal();
        debouncer.Debounce(() =>
        {
            Interlocked.Increment(ref executions);
            second.TrySetResult();
        });
        await second.Task.WaitAsync(WaitLimit);

        Assert.Equal(2, Volatile.Read(ref executions));
    }

    /// <summary>
    /// 释放会取消尚未到期的操作
    /// </summary>
    [Fact]
    public async Task Dispose_CancelsPendingAction()
    {
        var debouncer = new Debouncer(Interval);
        var signal = CreateSignal();

        debouncer.Debounce(() => signal.TrySetResult());
        debouncer.Dispose();

        var fired = await Task.WhenAny(signal.Task, Task.Delay(Interval * 10));

        Assert.NotSame(signal.Task, fired);
    }

    /// <summary>
    /// 释放之后再触发不抛异常，也不执行操作
    /// </summary>
    /// <remarks>
    /// 原实现会抛 ObjectDisposedException：Dispose 释放了内部令牌源，
    /// 而 Debounce 上来就对它调用 Cancel。
    /// </remarks>
    [Fact]
    public async Task Debounce_AfterDispose_IsIgnored()
    {
        var debouncer = new Debouncer(Interval);
        debouncer.Dispose();

        var signal = CreateSignal();
        var exception = Record.Exception(() => debouncer.Debounce(() => signal.TrySetResult()));

        var fired = await Task.WhenAny(signal.Task, Task.Delay(Interval * 10));

        Assert.Null(exception);
        Assert.NotSame(signal.Task, fired);
    }

    /// <summary>
    /// 重复释放安全
    /// </summary>
    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var debouncer = new Debouncer(Interval);

        debouncer.Dispose();

        Assert.Null(Record.Exception(debouncer.Dispose));
    }

    /// <summary>
    /// 操作为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void Debounce_WhenActionIsNull_Throws()
    {
        using var debouncer = new Debouncer(Interval);

        Assert.Throws<ArgumentNullException>(() => debouncer.Debounce(null!));
    }

    /// <summary>
    /// 创建用于等待动作执行的信号
    /// </summary>
    private static TaskCompletionSource CreateSignal()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
