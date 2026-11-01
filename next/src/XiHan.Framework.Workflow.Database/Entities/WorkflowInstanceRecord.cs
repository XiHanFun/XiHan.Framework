// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Database.Entities;

/// <summary>
/// 租户分区的工作流实例行。
/// </summary>
/// <remarks>
/// 实现 <see cref="IMultiTenant"/>：按当前租户写入与过滤，随租户分库。索引：<c>(ParentInstanceId)</c> 供引擎级联子实例；<c>(CorrelationId)</c> 供按关联 ID 启动时查重；<c>(TenantId, EndedAt)</c> 供逐个租户的保留期清理；<c>(TenantId, CreatedAt)</c> 供列表按创建时间倒序分页与时间范围过滤（共库时先按租户收窄）。其余过滤组合按应用的查询习惯自己加。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
[EntityIndex(nameof(ParentInstanceId))]
[EntityIndex(nameof(CorrelationId))]
[EntityIndex(nameof(TenantId), nameof(EndedAt))]
[EntityIndex(nameof(TenantId), nameof(CreatedAt))]
public sealed class WorkflowInstanceRecord : WorkflowInstanceRow, IMultiTenant
{
    /// <inheritdoc />
    [StringLength(128)]
    public string TenantId { get; set; } = string.Empty;
}
