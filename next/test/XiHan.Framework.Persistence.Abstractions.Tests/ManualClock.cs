// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// 可以往回拨的时钟：<c>FakeTimeProvider</c> 拒绝回到过去，而时钟回拨正是雪花生成器要处理的情形。读写用 <see cref="Volatile"/>，生成线程自旋读它时能看到测试线程的改动。
/// </summary>
/// <param name="start">起始时刻。</param>
internal sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    /// <summary>
    /// 当前时刻的 UTC 刻度。
    /// </summary>
    private long _ticks = start.UtcTicks;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => new(Volatile.Read(ref _ticks), TimeSpan.Zero);

    /// <summary>
    /// 拨到指定时刻，可以早于当前。
    /// </summary>
    public void Set(DateTimeOffset value) => Volatile.Write(ref _ticks, value.UtcTicks);
}
