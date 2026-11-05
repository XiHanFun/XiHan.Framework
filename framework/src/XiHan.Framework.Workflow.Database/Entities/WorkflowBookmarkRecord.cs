// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Database.Entities;

/// <summary>
/// 租户分区的书签行
/// </summary>
/// <remarks>
/// 实现 <see cref="IMultiTenant"/>：按当前租户写入与过滤，随租户分库。索引：<c>(InstanceId)</c> 供按实例读取与级联删除；<c>(Kind, Key)</c> 供按信号、用户任务受理人、子流程节点找书签；<c>(TenantId, DueAt)</c> 供逐个租户轮询到期书签
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
[EntityIndex(nameof(InstanceId))]
[EntityIndex(nameof(Kind), nameof(Key))]
[EntityIndex(nameof(TenantId), nameof(DueAt))]
public sealed class WorkflowBookmarkRecord : WorkflowBookmarkRow, IMultiTenant
{
    /// <inheritdoc />
    [StringLength(128)]
    public string TenantId { get; set; } = string.Empty;
}
