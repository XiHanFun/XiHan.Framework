// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Database.Entities;

/// <summary>
/// 工作流实例的存储行：<c>WorkflowInstanceQuery</c> 的每个过滤项都有对应的列，整份实例在 JSON 文档里；同刻排序以写入序号为次关键字。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowInstanceRecord : IEntity<string>
{
    /// <inheritdoc />
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 定义 ID。
    /// </summary>
    public string DefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// 定义名。
    /// </summary>
    public string DefinitionName { get; set; } = string.Empty;

    /// <summary>
    /// 定义版本。
    /// </summary>
    public int DefinitionVersion { get; set; } = 1;

    /// <summary>
    /// 状态名（<c>WorkflowInstanceStatus</c> 的名字）。
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 业务关联 ID。
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// 发起人。
    /// </summary>
    public string? StarterId { get; set; }

    /// <summary>
    /// 父实例 ID。
    /// </summary>
    public string? ParentInstanceId { get; set; }

    /// <summary>
    /// 租户。
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 结束时间。
    /// </summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>
    /// 写入序号：同刻排序的次关键字，首次写入时由存储分配，整体替换保留。
    /// </summary>
    public long Sequence { get; set; }

    /// <summary>
    /// 整份实例的 JSON；长文本列。
    /// </summary>
    public string Document { get; set; } = string.Empty;
}
