// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

namespace XiHan.Framework.Scheduling.Abstractions.Storage;

/// <summary>
/// 作业存储（RFC-0013）。
/// </summary>
/// <remarks>
/// 作业、触发器、运行历史与节点。Provider 实现；读出来的记录都是快照。不可达 → <see cref="SchedulingErrors.StoreUnavailable"/>，容量已满 → <see cref="SchedulingErrors.StoreCapacity"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IJobStore
{
    /// <summary>
    /// 存储是否跨实例共享。
    /// </summary>
    /// <remarks>
    /// 共享时抢占即集群唯一；不共享（进程内）时集群唯一的作业只在持有领导租约的实例上按计划运行。
    /// </remarks>
    bool IsShared { get; }

    /// <summary>
    /// 按名找作业。
    /// </summary>
    /// <remarks>
    /// 不存在为无值。
    /// </remarks>
    ValueTask<Result<Maybe<JobRecord>>> FindJobAsync(JobName name, CancellationToken cancellationToken);

    /// <summary>
    /// 全部作业，按名升序。
    /// </summary>
    ValueTask<Result<IReadOnlyList<JobRecord>>> ListJobsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 新增或整体替换作业的定义与暂停状态（不含触发器）。
    /// </summary>
    /// <remarks>
    /// 已有行的 <see cref="JobRecord.ActiveRunId"/> 由存储保留，只有 <see cref="TryBeginRunAsync"/>、<see cref="CompleteRunAsync"/> 与 <see cref="RecoverNodesAsync"/> 改它。
    /// </remarks>
    ValueTask<Result> SaveJobsAsync(IReadOnlyList<JobRecord> jobs, CancellationToken cancellationToken);

    /// <summary>
    /// 删除作业并级联其触发器。
    /// </summary>
    /// <remarks>
    /// 不存在的忽略。运行历史保留。
    /// </remarks>
    ValueTask<Result> DeleteJobsAsync(IReadOnlyList<JobName> names, CancellationToken cancellationToken);

    /// <summary>
    /// 触发器。
    /// </summary>
    /// <remarks>
    /// <paramref name="job"/> 为空取全部；按作业名、触发器名升序。
    /// </remarks>
    ValueTask<Result<IReadOnlyList<TriggerRecord>>> TriggersAsync(JobName? job, CancellationToken cancellationToken);

    /// <summary>
    /// 新增或整体替换触发器。
    /// </summary>
    ValueTask<Result> SaveTriggersAsync(IReadOnlyList<TriggerRecord> triggers, CancellationToken cancellationToken);

    /// <summary>
    /// 删除触发器。
    /// </summary>
    /// <remarks>
    /// 不存在的忽略。
    /// </remarks>
    ValueTask<Result> DeleteTriggersAsync(IReadOnlyList<TriggerKey> keys, CancellationToken cancellationToken);

    /// <summary>
    /// 原子地抢占到期的触发器。
    /// </summary>
    /// <remarks>
    /// <see cref="TriggerState.Waiting"/>、非本地、<c>NextFireAt</c> ≤ <paramref name="dueBefore"/> 的触发器按到期时刻升序、优先级降序取至多 <paramref name="limit"/> 条，改为 <see cref="TriggerState.Acquired"/>（<c>AcquiredBy</c> = <paramref name="nodeId"/>，<c>AcquiredAt</c> = <paramref name="now"/>）并返回；多个节点同时调用时每条触发器只被一个节点拿到。
    /// </remarks>
    ValueTask<Result<IReadOnlyList<TriggerRecord>>> AcquireDueAsync(DateTimeOffset dueBefore, int limit, string nodeId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// 写入运行记录并开始运行。
    /// </summary>
    /// <remarks>
    /// <paramref name="exclusive"/> 时原子地占用作业的 <see cref="JobRecord.ActiveRunId"/>，已被别的运行占用返回 <see langword="false"/>（记录不写入）。作业不存在 → <see cref="SchedulingErrors.JobNotFound"/>。
    /// </remarks>
    ValueTask<Result<bool>> TryBeginRunAsync(JobRunRecord run, bool exclusive, CancellationToken cancellationToken);

    /// <summary>
    /// 写回已结束的运行。
    /// </summary>
    /// <remarks>
    /// 释放它占用的 <see cref="JobRecord.ActiveRunId"/>，并把该作业 <see cref="TriggerState.Blocked"/> 的触发器改回 <see cref="TriggerState.Waiting"/>。
    /// </remarks>
    ValueTask<Result> CompleteRunAsync(JobRunRecord run, CancellationToken cancellationToken);

    /// <summary>
    /// 运行历史查询。
    /// </summary>
    ValueTask<Result<IReadOnlyList<JobRunRecord>>> RunsAsync(JobRunQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// 运行中的记录。
    /// </summary>
    /// <remarks>
    /// <paramref name="job"/> 为空取全部；按开始时刻升序。
    /// </remarks>
    ValueTask<Result<IReadOnlyList<JobRunRecord>>> RunningAsync(JobName? job, CancellationToken cancellationToken);

    /// <summary>
    /// 每个作业最近一次已结束的运行（没有的作业不在结果里）。
    /// </summary>
    ValueTask<Result<IReadOnlyList<JobRunRecord>>> LastRunsAsync(IReadOnlyList<JobName> jobs, CancellationToken cancellationToken);

    /// <summary>
    /// 请求取消运行：逐项结果，不在运行中 → <see cref="SchedulingErrors.RunNotFound"/>。
    /// </summary>
    ValueTask<IReadOnlyList<Result>> RequestCancelAsync(IReadOnlyList<string> runIds, CancellationToken cancellationToken);

    /// <summary>
    /// <paramref name="nodeId"/> 上被请求取消且仍在运行的运行 ID。
    /// </summary>
    ValueTask<Result<IReadOnlyList<string>>> CancelRequestedRunsAsync(string nodeId, CancellationToken cancellationToken);

    /// <summary>
    /// 删除在 <paramref name="before"/> 之前结束的运行记录。
    /// </summary>
    /// <remarks>
    /// 返回删除条数。
    /// </remarks>
    ValueTask<Result<int>> PurgeRunsAsync(DateTimeOffset before, CancellationToken cancellationToken);

    /// <summary>
    /// 写节点心跳（新增或更新）。
    /// </summary>
    ValueTask<Result> HeartbeatAsync(SchedulerNodeRecord node, CancellationToken cancellationToken);

    /// <summary>
    /// 全部节点，按 ID 升序。
    /// </summary>
    ValueTask<Result<IReadOnlyList<SchedulerNodeRecord>>> NodesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 恢复失联节点。
    /// </summary>
    /// <remarks>
    /// 它们 <see cref="TriggerState.Acquired"/> / <see cref="TriggerState.Blocked"/> 的触发器改回 <see cref="TriggerState.Waiting"/>（没有下一次时刻的补为 <paramref name="now"/>），<see cref="JobRunState.Running"/> 的运行记为 <see cref="JobRunState.Lost"/>（结束时刻 = <paramref name="now"/>）并释放其占用的 <see cref="JobRecord.ActiveRunId"/>，删除节点记录；返回标记为 Lost 的运行。
    /// </remarks>
    ValueTask<Result<IReadOnlyList<JobRunRecord>>> RecoverNodesAsync(IReadOnlyList<string> nodeIds, DateTimeOffset now, CancellationToken cancellationToken);
}
