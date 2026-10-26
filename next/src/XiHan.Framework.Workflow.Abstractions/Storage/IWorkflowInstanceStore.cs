// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions.Storage;

/// <summary>
/// 实例与节点实例存储；不按租户过滤，不做乐观并发（实例锁串行化写入），写入按批量。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IWorkflowInstanceStore
{
    /// <summary>
    /// 按 ID 找实例。
    /// </summary>
    ValueTask<Result<Maybe<WorkflowInstance>>> FindAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// 按 ID 批量找实例；不存在的不在结果里。
    /// </summary>
    ValueTask<Result<IReadOnlyList<WorkflowInstance>>> FindManyAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// 列表：按创建时间降序，最多 <see cref="WorkflowInstanceQuery.Limit"/> 条。
    /// </summary>
    ValueTask<Result<IReadOnlyList<WorkflowInstance>>> ListAsync(WorkflowInstanceQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// 新增或整体替换实例。
    /// </summary>
    ValueTask<Result> SaveAsync(IReadOnlyList<WorkflowInstance> instances, CancellationToken cancellationToken);

    /// <summary>
    /// 删除实例（级联节点实例）；不存在的忽略。
    /// </summary>
    ValueTask<Result> DeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// 按 ID 找节点实例。
    /// </summary>
    ValueTask<Result<Maybe<WorkflowNodeInstance>>> FindNodeInstanceAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// 匹配查询过滤项的实例总数（不受 <see cref="WorkflowInstanceQuery.Skip"/> 与 <see cref="WorkflowInstanceQuery.Limit"/> 影响）。
    /// </summary>
    ValueTask<Result<long>> CountAsync(WorkflowInstanceQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// 实例的全部节点实例：开始时间升序，同刻按创建顺序（补偿按它逆序）。
    /// </summary>
    ValueTask<Result<IReadOnlyList<WorkflowNodeInstance>>> NodeInstancesAsync(string instanceId, CancellationToken cancellationToken);

    /// <summary>
    /// 新增或整体替换节点实例。
    /// </summary>
    ValueTask<Result> SaveNodeInstancesAsync(IReadOnlyList<WorkflowNodeInstance> nodeInstances, CancellationToken cancellationToken);
}
