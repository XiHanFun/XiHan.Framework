// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Calendars;

/// <summary>
/// 日历登记表。
/// </summary>
/// <remarks>
/// 名字 → 日历。以实例注册进容器，特性与应用共同填充；同名重复登记是编程错误。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class JobCalendars
{
    /// <summary>
    /// 按名字索引的日历。
    /// </summary>
    private readonly Dictionary<string, IJobCalendar> _calendars = new(StringComparer.Ordinal);

    /// <summary>
    /// 已登记的名字（升序）。
    /// </summary>
    public IReadOnlyCollection<string> Names => _calendars.Keys.Order(StringComparer.Ordinal).ToList();

    /// <summary>
    /// 按名字找日历。
    /// </summary>
    /// <remarks>
    /// 未登记为无值。
    /// </remarks>
    public Maybe<IJobCalendar> Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _calendars.TryGetValue(name, out var calendar) ? Maybe.Some(calendar) : default;
    }

    /// <summary>
    /// 登记。
    /// </summary>
    /// <remarks>
    /// 名字须符合作业名文法。
    /// </remarks>
    public JobCalendars Add(string name, IJobCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        if (!Jobs.JobName.IsValid(name))
        {
            throw new ArgumentException($"Calendar name '{name}' is invalid: it must match the job name grammar.", nameof(name));
        }

        if (!_calendars.TryAdd(name, calendar))
        {
            throw new ArgumentException($"Calendar '{name}' is registered more than once.", nameof(name));
        }

        return this;
    }
}
