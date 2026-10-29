// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 用户任务视图。
/// </summary>
/// <remarks>
/// 一个 <c>UserTask</c> 书签加所属实例的信息；任务 ID 就是书签 ID。
/// </remarks>
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
    /// 认领人（<c>Any</c> 策略下被认领后）。
    /// </summary>
    public string? ClaimedBy { get; init; }

    /// <summary>
    /// 优先级。
    /// </summary>
    public int? Priority { get; init; }

    /// <summary>
    /// 截止时刻。
    /// </summary>
    public DateTimeOffset? DueAt { get; init; }

    /// <summary>
    /// 表单键。
    /// </summary>
    public string? FormKey { get; init; }

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
    /// 载荷里的字符串值。
    /// </summary>
    /// <remarks>
    /// 没有或不是字符串为空。
    /// </remarks>
    private static string? Text(JsonObject payload, string key)
        => payload.TryGetPropertyValue(key, out var value) && value is JsonValue scalar ? WorkflowValues.AsString(scalar) : null;

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
            ClaimedBy = Text(bookmark.Payload, UserTaskPayloadKeys.ClaimedBy),
            Priority = bookmark.Payload.TryGetPropertyValue(UserTaskPayloadKeys.Priority, out var priority) && WorkflowValues.TryConvert<int>(priority, out var priorityValue) ? priorityValue : null,
            DueAt = Text(bookmark.Payload, UserTaskPayloadKeys.DueAt) is { } due && DateTimeOffset.TryParse(due, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var dueValue) ? dueValue : null,
            FormKey = Text(bookmark.Payload, UserTaskPayloadKeys.FormKey),
            CorrelationId = bookmark.CorrelationId ?? instance.CorrelationId,
            StarterId = instance.StarterId,
            FormData = bookmark.Payload.TryGetPropertyValue(UserTaskPayloadKeys.FormData, out var form) ? form as JsonObject : null,
            CreatedAt = bookmark.CreatedAt,
            TenantId = bookmark.TenantId,
        };
    }
}
