// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 运行历史查询
/// </summary>
/// <remarks>
/// 过滤项都可空、同时给出的取交集；结果按计划时刻降序，跳过 <see cref="Skip"/> 条后最多 <see cref="Take"/> 条。分页越界不在构造时抛出：查询常由请求绑定而来，越界是调用方可以改正的输入，由 <see cref="Schedulers.IJobScheduler.RunsAsync"/> 返回 <c>SCHEDULING_VALIDATION_002</c>（HTTP 400）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JobRunQuery
{
    /// <summary>
    /// 上限的最大值
    /// </summary>
    public const int MaxTake = 1000;

    /// <summary>
    /// 作业名
    /// </summary>
    public JobName? Job { get; init; }

    /// <summary>
    /// 状态集合
    /// </summary>
    /// <remarks>
    /// 空视同未限定
    /// </remarks>
    public IReadOnlyList<JobRunState>? States { get; init; }

    /// <summary>
    /// 计划时刻下界（含）
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// 计划时刻上界（不含）
    /// </summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>
    /// 触发器名
    /// </summary>
    /// <remarks>
    /// 只取由该触发器引起的运行；手动运行没有触发器名
    /// </remarks>
    public string? Trigger { get; init; }

    /// <summary>
    /// 执行节点 ID
    /// </summary>
    public string? Node { get; init; }

    /// <summary>
    /// 是否有错误
    /// </summary>
    /// <remarks>
    /// <see langword="true"/> 只取带错误码的运行，<see langword="false"/> 只取不带错误码的，<see langword="null"/> 不限定
    /// </remarks>
    public bool? HasError { get; init; }

    /// <summary>
    /// 跳过条数
    /// </summary>
    /// <remarks>
    /// 默认 0，不能为负
    /// </remarks>
    public int Skip { get; init; }

    /// <summary>
    /// 最多返回条数，1 到 <see cref="MaxTake"/>
    /// </summary>
    /// <remarks>
    /// 默认 100
    /// </remarks>
    public int Take { get; init; } = 100;

    /// <summary>
    /// 记录是否匹配全部过滤项（不含分页）
    /// </summary>
    public bool Matches(JobRunRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return (Job is null || record.Job == Job.Value)
            && (States is not { Count: > 0 } || States.Contains(record.State))
            && (From is null || record.ScheduledAt >= From.Value)
            && (To is null || record.ScheduledAt < To.Value)
            && (Trigger is null || string.Equals(record.TriggerName, Trigger, StringComparison.Ordinal))
            && (Node is null || string.Equals(record.NodeId, Node, StringComparison.Ordinal))
            && (HasError is null || (record.ErrorCode is not null) == HasError.Value);
    }
}
