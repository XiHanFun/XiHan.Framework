// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Database.Entities;

/// <summary>
/// 书签存储行的公共列。
/// </summary>
/// <remarks>
/// 按实例、种类与键、信号与到期时间查询用的列，整份书签在 JSON 文档里。待办查询下推用的列：节点 ID 与任务标题取自书签，定义名、实例状态与实例一侧的关键字查询列取自所属实例，由存储在写书签、写实例时维护（ADR-0077、ADR-0096、ADR-0099）。两张表共用它：租户分区的 <see cref="WorkflowBookmarkRecord"/> 与平台分区的 <see cref="PlatformWorkflowBookmarkRecord"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class WorkflowBookmarkRow : IEntity<string>
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
    /// 节点 ID。
    /// </summary>
    /// <remarks>
    /// 可空只为给已有行的表加列（升级前写入的行为空，迁移时从文档回填）；存储写入的行总有值。
    /// </remarks>
    [StringLength(128)]
    public string? NodeId { get; set; }

    /// <summary>
    /// 所属实例的定义名。
    /// </summary>
    /// <remarks>
    /// 写书签时从实例行取；实例行还不存在的（定义级定时书签）为空。
    /// </remarks>
    [StringLength(256)]
    public string? DefinitionName { get; set; }

    /// <summary>
    /// 所属实例的状态（<c>WorkflowInstanceStatus</c> 的名字）。
    /// </summary>
    /// <remarks>
    /// 写书签时从实例行取，实例的状态变了由实例存储同步；待办查询只取 <c>Running</c> 的。
    /// </remarks>
    [StringLength(32)]
    public string? InstanceStatus { get; set; }

    /// <summary>
    /// 任务标题的关键字查询列。
    /// </summary>
    /// <remarks>
    /// 书签载荷 <c>title</c>（用户任务的标题）折叠后的值（<c>WorkflowKeyword.Fold</c>），载荷没有标题为空；写书签时由存储算出。可空也为给已有行的表加列（升级前写入的行要从文档回填，ADR-0096）。
    /// </remarks>
    [LongText]
    public string? SearchTitle { get; set; }

    /// <summary>
    /// 所属实例一侧的关键字查询列。
    /// </summary>
    /// <remarks>
    /// 实例标题、关联 ID、定义名各自折叠后以 U+001F 拼成：实例行的 <c>SearchText</c>（标题与关联 ID）再拼上折叠后的定义名（同 4.x 的待办匹配实例名称、定义编码与关联 ID，ADR-0099）。写书签时从实例行算出，实例新增或这几个字段变了由实例存储同步（与定义名、实例状态同一路径）；所属实例的行不存在、或实例行的 <c>SearchText</c> 还没回填时为空。
    /// </remarks>
    [LongText]
    public string? SearchInstanceText { get; set; }

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
