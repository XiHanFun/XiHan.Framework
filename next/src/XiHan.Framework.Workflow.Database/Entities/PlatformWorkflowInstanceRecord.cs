// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Database.Entities;

/// <summary>
/// 平台分区的工作流实例行。
/// </summary>
/// <remarks>
/// 无租户的调用（平台）启动的实例：不实现 <c>IMultiTenant</c>，落平台库，租户看不到。索引：<c>(ParentInstanceId)</c>、<c>(CorrelationId)</c>、<c>(EndedAt)</c>、<c>(CreatedAt)</c>、<c>(StarterId, CreatedAt)</c>，用途同 <see cref="WorkflowInstanceRecord"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
[EntityIndex(nameof(ParentInstanceId))]
[EntityIndex(nameof(CorrelationId))]
[EntityIndex(nameof(EndedAt))]
[EntityIndex(nameof(CreatedAt))]
[EntityIndex(nameof(StarterId), nameof(CreatedAt))]
public sealed class PlatformWorkflowInstanceRecord : WorkflowInstanceRow
{
}
