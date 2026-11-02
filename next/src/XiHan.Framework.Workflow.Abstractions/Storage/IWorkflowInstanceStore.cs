// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions.Storage;

/// <summary>
/// 实例与节点实例存储。
/// </summary>
/// <remarks>
/// 按分区读写：每个方法的第一个参数 <c>tenantId</c> 是分区（空为平台，否则是该租户），只返回这个分区的实例与节点实例，写入的实例 <see cref="WorkflowInstance.TenantId"/> 必须等于它；节点实例随所属实例的分区。不做乐观并发（实例锁串行化写入），写入按批量。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IWorkflowInstanceStore
{
    /// <summary>
    /// 按 ID 找实例。
    /// </summary>
    ValueTask<Result<Maybe<WorkflowInstance>>> FindAsync(string? tenantId, string id, CancellationToken cancellationToken);

    /// <summary>
    /// 按 ID 批量找实例。
    /// </summary>
    /// <remarks>
    /// 不存在或在别的分区的不在结果里。
    /// </remarks>
    ValueTask<Result<IReadOnlyList<WorkflowInstance>>> FindManyAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// 列表：按排序字段排序，最多 <see cref="WorkflowInstanceQuery.Limit"/> 条。
    /// </summary>
    /// <remarks>
    /// 过滤项见 <see cref="WorkflowInstanceQuery.Matches"/>：关联 ID 集合为空不匹配任何实例，只取顶层是父实例为空，关键字按 <see cref="WorkflowKeyword"/> 折叠后匹配实例标题或关联 ID（ADR-0096、ADR-0099）。按 <see cref="WorkflowInstanceQuery.SortBy"/> 与 <see cref="WorkflowInstanceQuery.Descending"/> 排序（缺省创建时间降序），再接次序键创建时间降序、写入先后降序（排序字段是创建时间时随它的方向），标题按 <see cref="WorkflowSortKeys.Title"/> 比较（ADR-0102）。分页、关联 ID 集合的上限与排序字段已由调用方校验：排序字段不在白名单里抛出 <see cref="ArgumentOutOfRangeException"/>。
    /// </remarks>
    ValueTask<Result<IReadOnlyList<WorkflowInstance>>> ListAsync(string? tenantId, WorkflowInstanceQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// 新增或整体替换实例。
    /// </summary>
    ValueTask<Result> SaveAsync(string? tenantId, IReadOnlyList<WorkflowInstance> instances, CancellationToken cancellationToken);

    /// <summary>
    /// 删除实例（级联节点实例）。
    /// </summary>
    /// <remarks>
    /// 不存在或在别的分区的忽略。
    /// </remarks>
    ValueTask<Result> DeleteAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// 按 ID 找节点实例。
    /// </summary>
    ValueTask<Result<Maybe<WorkflowNodeInstance>>> FindNodeInstanceAsync(string? tenantId, string id, CancellationToken cancellationToken);

    /// <summary>
    /// 匹配查询过滤项的实例总数（不受 <see cref="WorkflowInstanceQuery.Skip"/> 与 <see cref="WorkflowInstanceQuery.Limit"/> 影响）。
    /// </summary>
    ValueTask<Result<long>> CountAsync(string? tenantId, WorkflowInstanceQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// 实例的全部节点实例：开始时间升序，同刻按创建顺序（补偿按它逆序）。
    /// </summary>
    ValueTask<Result<IReadOnlyList<WorkflowNodeInstance>>> NodeInstancesAsync(string? tenantId, string instanceId, CancellationToken cancellationToken);

    /// <summary>
    /// 新增或整体替换节点实例。
    /// </summary>
    ValueTask<Result> SaveNodeInstancesAsync(string? tenantId, IReadOnlyList<WorkflowNodeInstance> nodeInstances, CancellationToken cancellationToken);
}
