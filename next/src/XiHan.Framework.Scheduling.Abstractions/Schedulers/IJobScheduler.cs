// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

namespace XiHan.Framework.Scheduling.Abstractions.Schedulers;

/// <summary>
/// 作业调度器（RFC-0013）。
/// </summary>
/// <remarks>
/// 登记、暂停、触发、状态与历史。全部批量、逐项结果。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IJobScheduler
{
    /// <summary>
    /// 新增或整体替换作业与触发器（来源 Runtime）。
    /// </summary>
    /// <remarks>
    /// 立即生效。作业类型未登记 → <see cref="SchedulingErrors.JobTypeNotRegistered"/>，定义不合法 → <see cref="SchedulingErrors.DefinitionInvalid"/>。
    /// </remarks>
    ValueTask<IReadOnlyList<Result>> ScheduleAsync(IReadOnlyList<JobDefinition> definitions, CancellationToken cancellationToken);

    /// <summary>
    /// 删除作业与触发器。
    /// </summary>
    /// <remarks>
    /// 进行中的运行照常结束。未登记 → <see cref="SchedulingErrors.JobNotFound"/>。
    /// </remarks>
    ValueTask<IReadOnlyList<Result>> UnscheduleAsync(IReadOnlyList<JobName> names, CancellationToken cancellationToken);

    /// <summary>
    /// 暂停作业的全部触发器。
    /// </summary>
    /// <remarks>
    /// 手动触发仍可用。
    /// </remarks>
    ValueTask<IReadOnlyList<Result>> PauseAsync(IReadOnlyList<JobName> names, CancellationToken cancellationToken);

    /// <summary>
    /// 恢复作业的全部触发器。
    /// </summary>
    /// <remarks>
    /// 暂停期间错过的按 misfire 策略处理。
    /// </remarks>
    ValueTask<IReadOnlyList<Result>> ResumeAsync(IReadOnlyList<JobName> names, CancellationToken cancellationToken);

    /// <summary>
    /// 暂停单个触发器。
    /// </summary>
    /// <remarks>
    /// 不存在 → <see cref="SchedulingErrors.TriggerNotFound"/>。
    /// </remarks>
    ValueTask<IReadOnlyList<Result>> PauseTriggersAsync(IReadOnlyList<TriggerKey> keys, CancellationToken cancellationToken);

    /// <summary>
    /// 恢复单个触发器。
    /// </summary>
    /// <remarks>
    /// 不存在 → <see cref="SchedulingErrors.TriggerNotFound"/>。
    /// </remarks>
    ValueTask<IReadOnlyList<Result>> ResumeTriggersAsync(IReadOnlyList<TriggerKey> keys, CancellationToken cancellationToken);

    /// <summary>
    /// 在本实例立即运行并等待结果，按顺序逐项。
    /// </summary>
    /// <remarks>
    /// 不受暂停与领导限制。未登记 → <see cref="SchedulingErrors.JobNotFound"/>，正在运行且不允许并发 → <see cref="SchedulingErrors.JobRunning"/>。
    /// </remarks>
    ValueTask<IReadOnlyList<JobTriggerResult>> TriggerAsync(IReadOnlyList<JobTriggerRequest> requests, CancellationToken cancellationToken);

    /// <summary>
    /// 入队一次立即到期的运行，由集群中任一节点运行，不等待。
    /// </summary>
    /// <remarks>
    /// 返回一次性触发器的名字。
    /// </remarks>
    ValueTask<IReadOnlyList<Result<string>>> EnqueueAsync(IReadOnlyList<JobTriggerRequest> requests, CancellationToken cancellationToken);

    /// <summary>
    /// 请求取消运行。
    /// </summary>
    /// <remarks>
    /// 本实例的运行立即取消其令牌，其他节点的在下一个轮询周期取消。不在运行中 → <see cref="SchedulingErrors.RunNotFound"/>。
    /// </remarks>
    ValueTask<IReadOnlyList<Result>> CancelRunsAsync(IReadOnlyList<string> runIds, CancellationToken cancellationToken);

    /// <summary>
    /// 一个作业的状态。
    /// </summary>
    /// <remarks>
    /// 未登记为无值。
    /// </remarks>
    ValueTask<Result<Maybe<JobStatus>>> StatusAsync(JobName name, CancellationToken cancellationToken);

    /// <summary>
    /// 全部作业的状态，按名字排序。
    /// </summary>
    ValueTask<Result<IReadOnlyList<JobStatus>>> StatusesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 运行历史。
    /// </summary>
    ValueTask<Result<IReadOnlyList<JobRunRecord>>> RunsAsync(JobRunQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// 运行历史的总数。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="RunsAsync"/> 同一组过滤项，不看分页（ADR-0053）。
    /// </remarks>
    ValueTask<Result<long>> CountRunsAsync(JobRunQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// 把运行时改过的代码作业恢复为代码定义。
    /// </summary>
    /// <remarks>
    /// 来源回到 <see cref="JobSource.Code"/>，保留暂停状态与同名触发器的进度；作业没有代码定义 → <see cref="SchedulingErrors.CodeDefinitionNotFound"/>（ADR-0053）。
    /// </remarks>
    ValueTask<IReadOnlyList<Result>> RestoreAsync(IReadOnlyList<JobName> names, CancellationToken cancellationToken);
}
