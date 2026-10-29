// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 运行历史查询。
/// </summary>
/// <remarks>
/// 过滤项都可空、同时给出的取交集；结果按计划时刻降序，跳过 <see cref="Skip"/> 条后最多 <see cref="Limit"/> 条。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JobRunQuery
{
    /// <summary>
    /// 上限的最大值。
    /// </summary>
    public const int MaxLimit = 1000;

    /// <summary>
    /// 作业名。
    /// </summary>
    public JobName? Job { get; init; }

    /// <summary>
    /// 状态集合。
    /// </summary>
    /// <remarks>
    /// 空视同未限定。
    /// </remarks>
    public IReadOnlyList<JobRunState>? States { get; init; }

    /// <summary>
    /// 计划时刻下界（含）。
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// 计划时刻上界（不含）。
    /// </summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>
    /// 跳过条数。
    /// </summary>
    /// <remarks>
    /// 默认 0。
    /// </remarks>
    public int Skip
    {
        get => field;
        init => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The skip must not be negative.");
    }

    /// <summary>
    /// 最多返回条数，1 到 <see cref="MaxLimit"/>。
    /// </summary>
    /// <remarks>
    /// 默认 100。
    /// </remarks>
    public int Limit
    {
        get => field;
        init => field = value is >= 1 and <= MaxLimit ? value : throw new ArgumentOutOfRangeException(nameof(value), value, $"The limit must be between 1 and {MaxLimit}.");
    } = 100;

    /// <summary>
    /// 记录是否匹配全部过滤项（不含分页）。
    /// </summary>
    public bool Matches(JobRunRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return (Job is null || record.Job == Job.Value)
            && (States is not { Count: > 0 } || States.Contains(record.State))
            && (From is null || record.ScheduledAt >= From.Value)
            && (To is null || record.ScheduledAt < To.Value);
    }
}
