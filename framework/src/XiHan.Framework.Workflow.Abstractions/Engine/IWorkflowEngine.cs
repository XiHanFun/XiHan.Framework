// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions.Engine;

/// <summary>
/// 引擎
/// </summary>
/// <remarks>
/// 启动实例、消费书签、发信号、控制实例。按标识的控制操作都是批量、逐项结果；单实例的写法见 <see cref="WorkflowEngineExtensions"/>。批量语义（任务与外部任务服务相同，ADR-0149）：
/// <list type="bullet">
/// <item>结果与入参等长、同序；空集合直接返回空结果，不开事务、不访问存储</item>
/// <item>重复的 ID 按入参顺序逐项施加，后一项看见前一项的效果</item>
/// <item>自己开调用时整批一个事务：逐项失败不回滚其余项，失败的项没有写入；提交失败时每一项都报告提交的错误。在调用方的工作单元里时并入调用方的事务</item>
/// <item>取消在任何时刻都整批回滚并抛出 <see cref="OperationCanceledException"/>：事务里不留下半批</item>
/// <item>引擎不强加批次上限，批次大小由调用方控制</item>
/// </list>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IWorkflowEngine
{
    /// <summary>
    /// 启动实例并执行第一个波次
    /// </summary>
    /// <remarks>
    /// 返回执行后的实例
    /// </remarks>
    ValueTask<Result<WorkflowInstance>> StartAsync(WorkflowStartRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 消费书签并恢复节点
    /// </summary>
    /// <remarks>
    /// <paramref name="expectedKey"/> 非空时必须与书签的键相同
    /// </remarks>
    ValueTask<Result<WorkflowInstance>> ResumeAsync(string bookmarkId, JsonObject? inputs, string? expectedKey, CancellationToken cancellationToken);

    /// <summary>
    /// 发信号：消费当前租户下匹配的全部 <c>Signal</c> 书签，返回消费数
    /// </summary>
    ValueTask<Result<int>> SignalAsync(string signalName, JsonObject? payload, string? correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// 挂起（只允许运行中）
    /// </summary>
    ValueTask<IReadOnlyList<Result<WorkflowInstance>>> SuspendAsync(IReadOnlyList<string> instanceIds, string? reason, CancellationToken cancellationToken);

    /// <summary>
    /// 恢复挂起的实例（只允许已挂起）
    /// </summary>
    /// <remarks>
    /// 到期书签由轮询接续
    /// </remarks>
    ValueTask<IReadOnlyList<Result<WorkflowInstance>>> ResumeInstancesAsync(IReadOnlyList<string> instanceIds, CancellationToken cancellationToken);

    /// <summary>
    /// 取消
    /// </summary>
    /// <remarks>
    /// 运行中、已挂起、已故障可取消；已结束的幂等返回。定义启用补偿时执行补偿
    /// </remarks>
    ValueTask<IReadOnlyList<Result<WorkflowInstance>>> CancelAsync(IReadOnlyList<string> instanceIds, string? reason, CancellationToken cancellationToken);

    /// <summary>
    /// 终止（同取消，不补偿）
    /// </summary>
    ValueTask<IReadOnlyList<Result<WorkflowInstance>>> TerminateAsync(IReadOnlyList<string> instanceIds, string? reason, CancellationToken cancellationToken);

    /// <summary>
    /// 重试故障实例（只允许有故障节点的已故障实例）
    /// </summary>
    ValueTask<IReadOnlyList<Result<WorkflowInstance>>> RetryAsync(IReadOnlyList<string> instanceIds, CancellationToken cancellationToken);

    /// <summary>
    /// 改实例变量（合并与删除）
    /// </summary>
    /// <remarks>
    /// 只允许 Running / Suspended → 否则 <see cref="WorkflowErrors.InstanceStatus"/>
    /// </remarks>
    ValueTask<IReadOnlyList<Result<WorkflowInstance>>> SetVariablesAsync(IReadOnlyList<WorkflowVariableUpdate> updates, CancellationToken cancellationToken);

    /// <summary>
    /// 跳转
    /// </summary>
    /// <remarks>
    /// 取消全部未结束节点与书签、清 Join 状态，从目标节点重新执行；允许 Running / Suspended / Faulted；目标不存在 → <see cref="WorkflowErrors.InputInvalid"/>
    /// </remarks>
    ValueTask<IReadOnlyList<Result<WorkflowInstance>>> JumpAsync(IReadOnlyList<WorkflowJump> jumps, CancellationToken cancellationToken);

    /// <summary>
    /// 迁移到同名的另一个已发布版本
    /// </summary>
    /// <remarks>
    /// 每个未结束节点在目标里必须存在且活动类型相同 → 否则 <see cref="WorkflowErrors.InputInvalid"/>；允许 Running / Suspended
    /// </remarks>
    ValueTask<IReadOnlyList<Result<WorkflowInstance>>> MigrateAsync(IReadOnlyList<WorkflowMigration> migrations, CancellationToken cancellationToken);

    /// <summary>
    /// 删除早于给定时刻结束的实例（含节点实例与书签）
    /// </summary>
    /// <remarks>
    /// 返回删除的实例数（最多 <see cref="WorkflowPurgeRequest.Limit"/> 条，需要时重复调用）。<see cref="WorkflowPurgeRequest.Limit"/> 不在 1 到 <see cref="WorkflowInstanceQuery.MaxTake"/> 之间 → <c>WORKFLOW_VALIDATION_002</c>（HTTP 400），不开事务
    /// </remarks>
    ValueTask<Result<int>> PurgeAsync(WorkflowPurgeRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 按 ID 找实例
    /// </summary>
    /// <remarks>
    /// 有租户的调用看不到别的租户的实例，为无值
    /// </remarks>
    ValueTask<Result<Maybe<WorkflowInstance>>> FindInstanceAsync(string instanceId, CancellationToken cancellationToken);

    /// <summary>
    /// 按条件列实例
    /// </summary>
    /// <remarks>
    /// 只列调用方分区的实例。过滤、排序与分页由存储执行（关联 ID 集合、只取顶层、关键字见 <see cref="WorkflowInstanceQuery"/>，ADR-0096、ADR-0099）。<see cref="WorkflowInstanceQuery.Skip"/> 为负或 <see cref="WorkflowInstanceQuery.Take"/> 不在 1 到 <see cref="WorkflowInstanceQuery.MaxTake"/> 之间 → <c>WORKFLOW_VALIDATION_002</c>（HTTP 400，消息键 <c>workflow.instance.query_page_invalid</c>）；<see cref="WorkflowInstanceQuery.CorrelationIds"/> 超过 <see cref="WorkflowInstanceQuery.MaxCorrelationIds"/> 个或有空白的 ID → 同一错误码（消息键 <c>workflow.instance.query_correlations_invalid</c>）；排序项 <see cref="WorkflowInstanceQuery.Sorts"/> 不合法 → 同一错误码：项数超过 <see cref="WorkflowInstanceQuery.MaxSorts"/>（消息键 <c>workflow.instance.query_sorts_too_many</c>），某项为空或字段不是 <see cref="WorkflowInstanceSortField"/> 里定义的值（<c>workflow.instance.query_sort_invalid</c>），同一字段出现两次（<c>workflow.instance.query_sort_duplicate</c>，ADR-0102、ADR-0104）；都不进存储；关联 ID 的空集合直接返回空列表
    /// </remarks>
    ValueTask<Result<IReadOnlyList<WorkflowInstance>>> ListInstancesAsync(WorkflowInstanceQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// 按条件数实例
    /// </summary>
    /// <remarks>
    /// 分区与过滤同 <see cref="ListInstancesAsync"/>，不看分页与排序、也不校验它们；关联 ID 集合越界同样返回 <c>WORKFLOW_VALIDATION_002</c>，空集合直接为 0
    /// </remarks>
    ValueTask<Result<long>> CountInstancesAsync(WorkflowInstanceQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// 实例的节点实例，开始时间升序
    /// </summary>
    /// <remarks>
    /// 实例不存在或看不到 → <see cref="WorkflowErrors.InstanceNotFound"/>
    /// </remarks>
    ValueTask<Result<IReadOnlyList<WorkflowNodeInstance>>> NodeInstancesAsync(string instanceId, CancellationToken cancellationToken);

    /// <summary>
    /// 实例的全部等待点（书签）摘要，创建时间升序
    /// </summary>
    /// <remarks>
    /// 只读：定时、信号、子流程、用户任务、外部任务、重试、超时、提醒等全部种类，实例详情的「待恢复」用它，不必经存储契约（ADR-0096）。实例不存在或在别的分区 → <see cref="WorkflowErrors.InstanceNotFound"/>
    /// </remarks>
    ValueTask<Result<IReadOnlyList<WorkflowBookmarkSummary>>> BookmarksAsync(string instanceId, CancellationToken cancellationToken);
}
