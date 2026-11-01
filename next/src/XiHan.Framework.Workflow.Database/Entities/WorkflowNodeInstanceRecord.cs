// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Database.Entities;

/// <summary>
/// 租户分区的节点实例行。
/// </summary>
/// <remarks>
/// 实现 <see cref="IMultiTenant"/>：与所属实例同一租户、同一个库。索引：<c>(InstanceId)</c> 供按实例读取与级联删除。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
[EntityIndex(nameof(InstanceId))]
public sealed class WorkflowNodeInstanceRecord : WorkflowNodeInstanceRow, IMultiTenant
{
    /// <inheritdoc />
    [StringLength(128)]
    public string TenantId { get; set; } = string.Empty;
}
