// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Database.Entities;

/// <summary>
/// 工作流实例的存储行。
/// </summary>
/// <remarks>
/// <c>WorkflowInstanceQuery</c> 的每个过滤项都有对应的列，整份实例在 JSON 文档里；同刻排序以写入序号为次关键字。索引：<c>(ParentInstanceId)</c> 供引擎级联子实例；<c>(CorrelationId)</c> 供按关联 ID 启动时查重；<c>(EndedAt)</c> 供保留期清理；<c>(CreatedAt)</c> 供列表按创建时间倒序分页与时间范围过滤。其余过滤组合按应用的查询习惯自己加。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
[EntityIndex(nameof(ParentInstanceId))]
[EntityIndex(nameof(CorrelationId))]
[EntityIndex(nameof(EndedAt))]
[EntityIndex(nameof(CreatedAt))]
public sealed class WorkflowInstanceRecord : IEntity<string>
{
    /// <inheritdoc />
    [StringLength(128)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 定义 ID。
    /// </summary>
    [StringLength(128)]
    public string DefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// 定义名。
    /// </summary>
    [StringLength(256)]
    public string DefinitionName { get; set; } = string.Empty;

    /// <summary>
    /// 定义版本。
    /// </summary>
    public int DefinitionVersion { get; set; } = 1;

    /// <summary>
    /// 状态名（<c>WorkflowInstanceStatus</c> 的名字）。
    /// </summary>
    [StringLength(32)]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 业务关联 ID。
    /// </summary>
    [StringLength(256)]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// 发起人。
    /// </summary>
    [StringLength(256)]
    public string? StarterId { get; set; }

    /// <summary>
    /// 父实例 ID。
    /// </summary>
    [StringLength(128)]
    public string? ParentInstanceId { get; set; }

    /// <summary>
    /// 租户。
    /// </summary>
    [StringLength(128)]
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
    /// 整份实例的 JSON。
    /// </summary>
    /// <remarks>
    /// 长文本列。
    /// </remarks>
    [LongText]
    public string Document { get; set; } = string.Empty;
}
