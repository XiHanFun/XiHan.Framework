// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Queries;

/// <summary>
/// 查询的时间范围提示（两端都含）：按时间分表 / 分区的 Provider 据此只扫描范围内的表。它不是过滤条件，见 <see cref="EntityQuery{T}.TimeRange"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record TimeRange
{
    /// <summary>
    /// 创建；<paramref name="from"/> 不能晚于 <paramref name="to"/>。
    /// </summary>
    public TimeRange(DateTimeOffset from, DateTimeOffset to)
    {
        if (from > to)
        {
            throw new ArgumentOutOfRangeException(nameof(to), to, $"'To' ({to:O}) must not be earlier than 'From' ({from:O}).");
        }

        From = from;
        To = to;
    }

    /// <summary>
    /// 起点（含）。
    /// </summary>
    public DateTimeOffset From { get; }

    /// <summary>
    /// 终点（含）。
    /// </summary>
    public DateTimeOffset To { get; }
}
