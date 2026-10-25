// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 用户任务视图：一个 <c>UserTask</c> 书签加所属实例的信息；任务 ID 就是书签 ID。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record UserTask
{
    /// <summary>
    /// 任务 ID（书签 ID）。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 实例 ID。
    /// </summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// 实例显示名。
    /// </summary>
    public string? InstanceTitle { get; init; }

    /// <summary>
    /// 定义名。
    /// </summary>
    public required WorkflowName DefinitionName { get; init; }

    /// <summary>
    /// 节点 ID。
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// 节点实例 ID。
    /// </summary>
    public required string NodeInstanceId { get; init; }

    /// <summary>
    /// 任务标题。
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 指派人。
    /// </summary>
    public required string AssigneeId { get; init; }

    /// <summary>
    /// 关联 ID（书签的，否则实例的）。
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// 发起人。
    /// </summary>
    public string? StarterId { get; init; }

    /// <summary>
    /// 表单数据。
    /// </summary>
    public JsonObject? FormData { get; init; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 租户。
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// 从书签与实例构造。
    /// </summary>
    public static UserTask From(WorkflowBookmark bookmark, WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(bookmark);
        ArgumentNullException.ThrowIfNull(instance);
        return new UserTask
        {
            Id = bookmark.Id,
            InstanceId = instance.Id,
            InstanceTitle = instance.Title,
            DefinitionName = instance.DefinitionName,
            NodeId = bookmark.NodeId,
            NodeInstanceId = bookmark.NodeInstanceId,
            Title = bookmark.Payload.TryGetPropertyValue(UserTaskPayloadKeys.Title, out var title) && title is JsonValue value ? WorkflowValues.AsString(value) : null,
            AssigneeId = bookmark.Key ?? string.Empty,
            CorrelationId = bookmark.CorrelationId ?? instance.CorrelationId,
            StarterId = instance.StarterId,
            FormData = bookmark.Payload.TryGetPropertyValue(UserTaskPayloadKeys.FormData, out var form) ? form as JsonObject : null,
            CreatedAt = bookmark.CreatedAt,
            TenantId = bookmark.TenantId,
        };
    }
}
