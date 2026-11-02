// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;

namespace XiHan.Framework.Workflow.Database.Entities;

/// <summary>
/// 工作流实例存储行的公共列。
/// </summary>
/// <remarks>
/// <c>WorkflowInstanceQuery</c> 的每个过滤项都有对应的列（关键字是折叠后的 <see cref="SearchText"/>，只取顶层是 <see cref="ParentInstanceId"/> 为空），整份实例在 JSON 文档里；同刻排序以写入序号为次关键字。两张表共用它：租户分区的 <see cref="WorkflowInstanceRecord"/> 与平台分区的 <see cref="PlatformWorkflowInstanceRecord"/>。
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
    /// 状态在生命周期里的序号。
    /// </summary>
    /// <remarks>
    /// <c>Running</c> 1、<c>Suspended</c> 2、<c>Completed</c> 3、<c>Canceled</c> 4、<c>Faulted</c> 5、<c>Terminated</c> 6（同 4.x 按状态枚举的整数值排，ADR-0103）。按状态排序时 SQL 对它 <c>ORDER BY</c>，而不是对状态名（那是字母序）。存储每次整行写入时随 <see cref="Status"/> 一起写（新增、状态变更、迁移、跳转、重试都经整行写入），是存储内部的细节，不是公开常量。可空只为给已有行的表加列：升级前写入的行为空，迁移时按 <see cref="Status"/> 回填，没回填的行按状态排序时位置照数据库的空值规则。
    /// </remarks>
    public int? StatusOrder { get; set; }

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
    /// 关键字查询列。
    /// </summary>
    /// <remarks>
    /// 实例标题（显示名，没有取定义名）与关联 ID 各自折叠后以 U+001F 拼成（<c>WorkflowKeyword.Join</c>，关联 ID 为空按空串拼入），实例列表的关键字在 SQL 里对它做包含判断（同 4.x 匹配实例名称与关联 ID，ADR-0099）；书签行的 <c>SearchInstanceText</c> 由它再拼上折叠后的定义名。存储写入的行总有值；可空只为给已有行的表加列（升级前写入的行为空，迁移时从文档回填，ADR-0096）。
    /// </remarks>
    [LongText]
    public string? SearchText { get; set; }

    /// <summary>
    /// 排序用的标题。
    /// </summary>
    /// <remarks>
    /// 实例标题（显示名，没有取定义名）的前 256 个字符（<c>WorkflowSortKeys.Title</c>），按标题排序时 SQL 对它 <c>ORDER BY</c>（ADR-0102）；定长是因为长文本在 Oracle 上不能排序。存储写入的行总有值；可空只为给已有行的表加列（升级前写入的行为空，迁移时从文档回填，没回填的行按标题排序时位置照数据库的空值规则）。
    /// </remarks>
    [StringLength(WorkflowSortKeys.MaxTitleLength)]
    public string? Title { get; set; }

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
