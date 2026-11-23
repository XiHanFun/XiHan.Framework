// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Threading.Channels;
using Microsoft.Extensions.Time.Testing;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;

/// <summary>
/// 每建一个定时器就发一次信号的假时钟
/// </summary>
/// <remarks>
/// 被测代码在后台线程上等退避：用例要先确认它的定时器已经挂到时钟上再拨钟，否则拨过去的时间不算数、定时器永远不触发
/// </remarks>
internal sealed class TimerSignalingClock : FakeTimeProvider
{
    /// <summary>
    /// 已建而用例还没确认的定时器
    /// </summary>
    private readonly Channel<TimeSpan> _created = Channel.CreateUnbounded<TimeSpan>();

    /// <summary>
    /// 从当前时刻起的假时钟
    /// </summary>
    public TimerSignalingClock()
        : base(DateTimeOffset.UtcNow)
    {
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);
        _created.Writer.TryWrite(dueTime);
        return timer;
    }

    /// <summary>
    /// 等下一个定时器挂上，返回它的到期时长
    /// </summary>
    public async Task<TimeSpan> TimerCreatedAsync()
    {
        // 缺陷存在时不让用例挂死
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        return await _created.Reader.ReadAsync(limit.Token).ConfigureAwait(false);
    }
}
