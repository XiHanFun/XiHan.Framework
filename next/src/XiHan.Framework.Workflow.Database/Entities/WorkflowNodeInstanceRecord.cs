// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Database.Entities;

/// <summary>
/// 节点实例的存储行：按实例查询与按开始时间排序用的列，整份节点实例在 JSON 文档里；删实例时级联删除。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowNodeInstanceRecord : IEntity<string>
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
    /// 开始时间。
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// 写入序号：同刻排序的次关键字，首次写入时由存储分配，整体替换保留。
    /// </summary>
    public long Sequence { get; set; }

    /// <summary>
    /// 整份节点实例的 JSON；长文本列。
    /// </summary>
    [LongText]
    public string Document { get; set; } = string.Empty;
}
