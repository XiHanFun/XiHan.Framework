// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Validation;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 运行历史查询
/// </summary>
/// <remarks>
/// 过滤项都可空、同时给出的取交集；结果按计划时刻降序，跳过 <see cref="Skip"/> 条后最多 <see cref="Take"/> 条。分页越界不在构造时抛出：查询常由请求绑定而来，越界是调用方可以改正的输入，由 <see cref="Schedulers.IJobScheduler.RunsAsync"/> 返回 <c>SCHEDULING_VALIDATION_002</c>（HTTP 400）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record JobRunQuery
{
    /// <summary>
    /// 本版查询条件的修订号
    /// </summary>
    /// <remarks>
    /// Provider 在入口以 <c>Validate(JobRunQuery.Revision)</c> 传入，常量在编译时写进 Provider，遇到更新的条件即失败；以后新增条件时加 1，新条件记在新修订号下（RFC-0001「Provider 契约的演进」，ADR-0228）。修订 1：5.0 的全部条件
    /// </remarks>
    public const int Revision = 1;

    /// <summary>
    /// 这次查询用到的条件里最高的修订号
    /// </summary>
    /// <remarks>
    /// 没有用到新条件时不变，旧 Provider 照常服务。5.0 的条件都属于修订 1；以后新增条件时改为实例方法，按设置了的条件取其中最高的修订号，不要直接返回 <see cref="Revision"/>
    /// </remarks>
    private static int RequiredRevision() => 1;

    /// <summary>
    /// 核对这次用到的条件 Provider 是否都认识
    /// </summary>
    /// <param name="supportedRevision">Provider 编译时取到的 <see cref="Revision"/></param>
    /// <returns>成功；用到了更新的条件时为 <c>KERNEL_VALIDATION_007</c></returns>
    public Result Validate(int supportedRevision) => QueryRevision.Check(nameof(JobRunQuery), RequiredRevision(), supportedRevision);

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
    /// 只取由该触发器引起的运行；手动运行没有触发器名。与 <see cref="JobRunRecord.TriggerName"/> 同名（ADR-0161）
    /// </remarks>
    public string? TriggerName { get; init; }

    /// <summary>
    /// 触发来源集合
    /// </summary>
    /// <remarks>
    /// 只取由这些来源引起的运行（按计划、手动、恢复、重试）；空视同未限定。与 <see cref="JobRunRecord.Trigger"/> 同名（ADR-0161）
    /// </remarks>
    public IReadOnlyList<JobTrigger>? Triggers { get; init; }

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
    [ApiLevel(Stability.Preview, "5.0")]
    public bool Matches(JobRunRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return (Job is null || record.Job == Job.Value)
            && (States is not { Count: > 0 } || States.Contains(record.State))
            && (From is null || record.ScheduledAt >= From.Value)
            && (To is null || record.ScheduledAt < To.Value)
            && (TriggerName is null || string.Equals(record.TriggerName, TriggerName, StringComparison.Ordinal))
            && (Triggers is not { Count: > 0 } || Triggers.Contains(record.Trigger))
            && (Node is null || string.Equals(record.NodeId, Node, StringComparison.Ordinal))
            && (HasError is null || (record.ErrorCode is not null) == HasError.Value);
    }
}
