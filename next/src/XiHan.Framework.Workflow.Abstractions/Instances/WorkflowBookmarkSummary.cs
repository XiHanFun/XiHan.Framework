// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Instances;

/// <summary>
/// 实例的一个等待点（书签）的摘要。
/// </summary>
/// <remarks>
/// 面向应用的只读视图（实例详情里的「待恢复」）：节点在等什么（<see cref="Kind"/> 与 <see cref="Key"/>）、何时到期。不含载荷——载荷是活动与引擎的私有状态（用户任务的表单数据、外部任务的锁、提醒的排期等），用户任务的详情用 <c>IWorkflowTaskService.PendingByInstanceAsync</c>（ADR-0096）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowBookmarkSummary
{
    /// <summary>
    /// 书签 ID。
    /// </summary>
    /// <remarks>
    /// 用户任务的任务 ID 就是它。
    /// </remarks>
    public required string Id { get; init; }

    /// <summary>
    /// 所属实例 ID。
    /// </summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// 节点 ID。
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// 节点实例 ID。
    /// </summary>
    /// <remarks>
    /// 实例级超时书签是实例 ID。
    /// </remarks>
    public required string NodeInstanceId { get; init; }

    /// <summary>
    /// 种类（<see cref="BookmarkKinds"/>）。
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// 键：用户任务是指派人，信号是信号名，子流程是父节点实例 ID，外部任务是主题。
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// 到期时间（定时、重试、超时、提醒书签）。
    /// </summary>
    public DateTimeOffset? DueAt { get; init; }

    /// <summary>
    /// 关联 ID（信号书签限定关联）。
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 从书签取摘要。
    /// </summary>
    public static WorkflowBookmarkSummary From(WorkflowBookmark bookmark)
    {
        ArgumentNullException.ThrowIfNull(bookmark);
        return new WorkflowBookmarkSummary
        {
            Id = bookmark.Id,
            InstanceId = bookmark.InstanceId,
            NodeId = bookmark.NodeId,
            NodeInstanceId = bookmark.NodeInstanceId,
            Kind = bookmark.Kind,
            Key = bookmark.Key,
            DueAt = bookmark.DueAt,
            CorrelationId = bookmark.CorrelationId,
            CreatedAt = bookmark.CreatedAt,
        };
    }
}
