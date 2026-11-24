// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Diagnostics;

/// <summary>
/// 固定窗口的日志限频门：一个窗口里只放行一条，其余只计数
/// </summary>
/// <remarks>
/// 无锁，状态只有窗口的起点与被压掉的条数两个字段，大小与进入的次数、进入的是谁无关。窗口从放行的那一条算起，按 <see cref="TimeProvider.GetTimestamp"/> 的单调时间计，不受系统时间调整影响；窗口过后的第一条放行，并交出上一次放行以来被压掉的条数
/// </remarks>
internal sealed class LogRateGate
{
    /// <summary>
    /// 还没有放行过任何一条时窗口起点的取值
    /// </summary>
    private const long Never = long.MinValue;

    /// <summary>
    /// 时钟
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 窗口的长度
    /// </summary>
    private readonly TimeSpan _window;

    /// <summary>
    /// 当前窗口的起点（时钟的时间戳）
    /// </summary>
    private long _windowStart = Never;

    /// <summary>
    /// 上一次放行以来被压掉的条数
    /// </summary>
    private long _suppressed;

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="clock">时钟</param>
    /// <param name="window">窗口的长度，必须为正</param>
    public LogRateGate(TimeProvider clock, TimeSpan window)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);
        _clock = clock;
        _window = window;
    }

    /// <summary>
    /// 这一条能不能记
    /// </summary>
    /// <remarks>
    /// 可以在多个线程上同时调用：同一个窗口只有一个调用得到 <see langword="true"/>
    /// </remarks>
    /// <param name="suppressed">放行时是上一次放行以来被压掉的条数，否则为 0</param>
    public bool TryEnter(out long suppressed)
    {
        var now = _clock.GetTimestamp();
        var start = Volatile.Read(ref _windowStart);

        // 窗口还开着：只计数。另一个线程刚把起点换成比 now 更晚的时间戳时间隔为负，同样落在这里
        if (start != Never && _clock.GetElapsedTime(start, now) < _window)
        {
            Interlocked.Increment(ref _suppressed);
            suppressed = 0;
            return false;
        }

        // 窗口已过或还没开过：以比较交换抢新窗口的起点，只有抢到的那一条放行；
        // 没抢到说明别的线程刚开了新窗口，这一条算在它的窗口里被压掉
        if (Interlocked.CompareExchange(ref _windowStart, now, start) != start)
        {
            Interlocked.Increment(ref _suppressed);
            suppressed = 0;
            return false;
        }

        // 取走并清零。计数与换起点不是一个原子动作：同时到的线程里，已经加了一的算进这一条，此刻才加一的留给下一次放行；
        // 单条上的数因此可能在相邻两条之间挪动几个，合计不丢也不重
        suppressed = Interlocked.Exchange(ref _suppressed, 0);
        return true;
    }
}
