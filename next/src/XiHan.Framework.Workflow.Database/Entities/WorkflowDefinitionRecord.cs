// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Database.Entities;

/// <summary>
/// 租户分区的工作流定义行。
/// </summary>
/// <remarks>
/// 实现 <see cref="IMultiTenant"/>：持久化按当前租户写入与过滤，租户有独立库时落在它的分片连接上（RFC-0007「分片」）。索引：<c>(Name, TenantId, Version)</c> 唯一，是分区里的业务键，按名字与版本查找、取最大版本与最新发布版本都走它；租户列不为空，唯一性对每一行都成立。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
[EntityIndex(nameof(Name), nameof(TenantId), nameof(Version), IsUnique = true)]
public sealed class WorkflowDefinitionRecord : WorkflowDefinitionRow, IMultiTenant
{
    /// <inheritdoc />
    [StringLength(128)]
    public string TenantId { get; set; } = string.Empty;
}
