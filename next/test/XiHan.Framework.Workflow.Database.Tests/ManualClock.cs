// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 只由用例推进的时钟。
/// </summary>
/// <remarks>
/// 租约的到期与等待上限按它判定；只替换 <see cref="GetUtcNow"/>，重试与续约的间隔仍是系统计时器。
/// </remarks>
internal sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    /// <summary>
    /// 推进与读取的互斥。
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 当前时刻。
    /// </summary>
    private DateTimeOffset _now = start;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    /// <summary>
    /// 往后推进。
    /// </summary>
    public void Advance(TimeSpan by)
    {
        lock (_gate)
        {
            _now += by;
        }
    }
}
