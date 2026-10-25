// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Conformance.Fixtures;

/// <summary>
/// 套件用的可拨动时钟。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class ConformanceClock(DateTimeOffset start) : TimeProvider
{
    /// <summary>
    /// 当前时刻；只由 <see cref="Advance"/> 推进，不随真实时间流动。
    /// </summary>
    private DateTimeOffset _now = start;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>
    /// 前进。
    /// </summary>
    public void Advance(TimeSpan by) => _now += by;
}
