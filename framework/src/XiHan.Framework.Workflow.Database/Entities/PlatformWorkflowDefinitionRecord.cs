// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Database.Entities;

/// <summary>
/// 平台分区的工作流定义行
/// </summary>
/// <remarks>
/// 无租户的调用（平台）建的定义：不实现 <c>IMultiTenant</c>，始终落默认连接（平台库），租户看不到。索引：<c>(Name, Version)</c> 唯一
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
[EntityIndex(nameof(Name), nameof(Version), IsUnique = true)]
public sealed class PlatformWorkflowDefinitionRecord : WorkflowDefinitionRow
{
}
