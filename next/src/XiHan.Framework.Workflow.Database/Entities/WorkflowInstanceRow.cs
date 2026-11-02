// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Database.Entities;

/// <summary>
/// 工作流实例存储行的公共列。
/// </summary>
/// <remarks>
/// <c>WorkflowInstanceQuery</c> 的每个过滤项都有对应的列（标题关键字是折叠后的 <see cref="SearchTitle"/>，只取顶层是 <see cref="ParentInstanceId"/> 为空），整份实例在 JSON 文档里；同刻排序以写入序号为次关键字。两张表共用它：租户分区的 <see cref="WorkflowInstanceRecord"/> 与平台分区的 <see cref="PlatformWorkflowInstanceRecord"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class WorkflowInstanceRow : IEntity<string>
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
    /// 标题关键字查询列。
    /// </summary>
    /// <remarks>
    /// 实例标题（显示名，没有取定义名）折叠后的值（<c>WorkflowKeyword.Fold</c>），实例列表的关键字在 SQL 里对它做包含判断；书签行的 <c>SearchInstanceTitle</c> 也取自它。存储写入的行总有值；可空只为给已有行的表加列（升级前写入的行为空，迁移时从文档回填，ADR-0096）。
    /// </remarks>
    [LongText]
    public string? SearchTitle { get; set; }

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
