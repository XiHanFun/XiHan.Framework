// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Schedules;

/// <summary>
/// 作业的计划：手动、固定间隔或 cron。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JobSchedule
{
    /// <summary>
    /// 由种类与参数创建。
    /// </summary>
    private JobSchedule(JobScheduleKind kind, TimeSpan interval, CronExpression? cron)
    {
        Kind = kind;
        Interval = interval;
        Cron = cron;
    }

    /// <summary>
    /// 只手动触发。
    /// </summary>
    public static JobSchedule Manual { get; } = new(JobScheduleKind.Manual, TimeSpan.Zero, null);

    /// <summary>
    /// 种类。
    /// </summary>
    public JobScheduleKind Kind { get; }

    /// <summary>
    /// 间隔（只对 <see cref="JobScheduleKind.Interval"/> 有意义）。
    /// </summary>
    public TimeSpan Interval { get; }

    /// <summary>
    /// cron 表达式（只对 <see cref="JobScheduleKind.Cron"/> 有值）。
    /// </summary>
    public CronExpression? Cron { get; }

    /// <summary>
    /// 固定间隔；必须为正。
    /// </summary>
    public static JobSchedule Every(TimeSpan interval)
        => interval > TimeSpan.Zero ? new JobSchedule(JobScheduleKind.Interval, interval, null) : throw new ArgumentOutOfRangeException(nameof(interval), interval, "The interval must be positive.");

    /// <summary>
    /// cron 表达式；不合法抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public static JobSchedule FromCron(string expression) => new(JobScheduleKind.Cron, TimeSpan.Zero, CronExpression.Parse(expression));

    /// <summary>
    /// <paramref name="after"/> 之后的下一次运行时刻；手动计划无值。
    /// </summary>
    public Maybe<DateTimeOffset> NextRun(DateTimeOffset after) => Kind switch
    {
        JobScheduleKind.Interval => after + Interval,
        JobScheduleKind.Cron => Cron!.Next(after),
        _ => default(Maybe<DateTimeOffset>),
    };

    /// <inheritdoc />
    public override string ToString() => Kind switch
    {
        JobScheduleKind.Interval => "every " + Interval,
        JobScheduleKind.Cron => "cron " + Cron,
        _ => "manual",
    };
}
