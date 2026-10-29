// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions.Engine;

/// <summary>
/// 引擎。
/// </summary>
/// <remarks>
/// 启动实例、消费书签、发信号、控制实例。按标识的控制操作都是批量、逐项结果；单实例的写法见 <see cref="WorkflowEngineExtensions"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IWorkflowEngine
{
    /// <summary>
    /// 启动实例并执行第一个波次。
    /// </summary>
    /// <remarks>
    /// 返回执行后的实例。
    /// </remarks>
    ValueTask<Result<WorkflowInstance>> StartAsync(WorkflowStartRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 消费书签并恢复节点。
    /// </summary>
    /// <remarks>
    /// <paramref name="expectedKey"/> 非空时必须与书签的键相同。
    /// </remarks>
    ValueTask<Result<WorkflowInstance>> ResumeAsync(string bookmarkId, JsonObject? inputs, string? expectedKey, CancellationToken cancellationToken);

    /// <summary>
    /// 发信号：消费当前租户下匹配的全部 <c>Signal</c> 书签，返回消费数。
    /// </summary>
    ValueTask<Result<int>> SignalAsync(string signalName, JsonObject? payload, string? correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// 挂起（只允许运行中）。
    /// </summary>
    ValueTask<IReadOnlyList<Result<WorkflowInstance>>> SuspendAsync(IReadOnlyList<string> instanceIds, string? reason, CancellationToken cancellationToken);

    /// <summary>
    /// 恢复挂起的实例（只允许已挂起）。
    /// </summary>
    /// <remarks>
    /// 到期书签由轮询接续。
    /// </remarks>
    ValueTask<IReadOnlyList<Result<WorkflowInstance>>> ResumeInstancesAsync(IReadOnlyList<string> instanceIds, CancellationToken cancellationToken);

    /// <summary>
    /// 取消。
    /// </summary>
    /// <remarks>
    /// 运行中、已挂起、已故障可取消；已结束的幂等返回。定义启用补偿时执行补偿。
    /// </remarks>
    ValueTask<IReadOnlyList<Result<WorkflowInstance>>> CancelAsync(IReadOnlyList<string> instanceIds, string? reason, CancellationToken cancellationToken);

    /// <summary>
    /// 终止（同取消，不补偿）。
    /// </summary>
    ValueTask<IReadOnlyList<Result<WorkflowInstance>>> TerminateAsync(IReadOnlyList<string> instanceIds, string? reason, CancellationToken cancellationToken);

    /// <summary>
    /// 重试故障实例（只允许有故障节点的已故障实例）。
    /// </summary>
    ValueTask<IReadOnlyList<Result<WorkflowInstance>>> RetryAsync(IReadOnlyList<string> instanceIds, CancellationToken cancellationToken);

    /// <summary>
    /// 改实例变量（合并与删除）。
    /// </summary>
    /// <remarks>
    /// 只允许 Running / Suspended → 否则 <see cref="WorkflowErrors.InstanceStatus"/>。
    /// </remarks>
    ValueTask<IReadOnlyList<Result<WorkflowInstance>>> SetVariablesAsync(IReadOnlyList<WorkflowVariableUpdate> updates, CancellationToken cancellationToken);

    /// <summary>
    /// 跳转。
    /// </summary>
    /// <remarks>
    /// 取消全部未结束节点与书签、清 Join 状态，从目标节点重新执行；允许 Running / Suspended / Faulted；目标不存在 → <see cref="WorkflowErrors.InputInvalid"/>。
    /// </remarks>
    ValueTask<IReadOnlyList<Result<WorkflowInstance>>> JumpAsync(IReadOnlyList<WorkflowJump> jumps, CancellationToken cancellationToken);

    /// <summary>
    /// 迁移到同名的另一个已发布版本。
    /// </summary>
    /// <remarks>
    /// 每个未结束节点在目标里必须存在且活动类型相同 → 否则 <see cref="WorkflowErrors.InputInvalid"/>；允许 Running / Suspended。
    /// </remarks>
    ValueTask<IReadOnlyList<Result<WorkflowInstance>>> MigrateAsync(IReadOnlyList<WorkflowMigration> migrations, CancellationToken cancellationToken);

    /// <summary>
    /// 删除早于给定时刻结束的实例（含节点实例与书签）。
    /// </summary>
    /// <remarks>
    /// 返回删除的实例数（最多 <see cref="WorkflowPurgeRequest.Limit"/> 条，需要时重复调用）。
    /// </remarks>
    ValueTask<Result<int>> PurgeAsync(WorkflowPurgeRequest request, CancellationToken cancellationToken);
}
