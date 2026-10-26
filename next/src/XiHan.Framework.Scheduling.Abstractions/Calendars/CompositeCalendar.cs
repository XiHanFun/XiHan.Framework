// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Calendars;

/// <summary>
/// 组合日历：任一成员排除即排除。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class CompositeCalendar : IJobCalendar
{
    /// <summary>
    /// 成员。
    /// </summary>
    private readonly IReadOnlyList<IJobCalendar> _calendars;

    /// <summary>
    /// 创建。
    /// </summary>
    public CompositeCalendar(params IReadOnlyList<IJobCalendar> calendars)
    {
        ArgumentNullException.ThrowIfNull(calendars);
        _calendars = [.. calendars];
    }

    /// <summary>
    /// 成员。
    /// </summary>
    public IReadOnlyList<IJobCalendar> Calendars => _calendars;

    /// <inheritdoc />
    public bool IsExcluded(DateTimeOffset at)
    {
        foreach (var calendar in _calendars)
        {
            if (calendar.IsExcluded(at))
            {
                return true;
            }
        }

        return false;
    }
}
