// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Database.Entities;

/// <summary>
/// 书签的存储行。
/// </summary>
/// <remarks>
/// 按实例、种类与键、信号与到期时间查询用的列，整份书签在 JSON 文档里。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowBookmarkRecord : IEntity<string>
{
    /// <inheritdoc />
    [StringLength(128)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 所属实例 ID。
    /// </summary>
    [StringLength(128)]
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 节点实例 ID。
    /// </summary>
    [StringLength(128)]
    public string NodeInstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 种类（<c>BookmarkKinds</c>）。
    /// </summary>
    [StringLength(64)]
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 键：用户任务是指派人，信号是信号名，子流程是父节点实例 ID。
    /// </summary>
    [StringLength(256)]
    public string? Key { get; set; }

    /// <summary>
    /// 关联 ID。
    /// </summary>
    [StringLength(256)]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// 到期时间。
    /// </summary>
    public DateTimeOffset? DueAt { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 租户。
    /// </summary>
    [StringLength(128)]
    public string? TenantId { get; set; }

    /// <summary>
    /// 写入序号：同刻排序的次关键字，首次写入时由存储分配，整体替换保留。
    /// </summary>
    public long Sequence { get; set; }

    /// <summary>
    /// 整份书签的 JSON。
    /// </summary>
    /// <remarks>
    /// 长文本列。
    /// </remarks>
    [LongText]
    public string Document { get; set; } = string.Empty;
}
