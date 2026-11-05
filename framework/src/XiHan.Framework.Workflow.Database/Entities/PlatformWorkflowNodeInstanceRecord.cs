// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Database.Entities;

/// <summary>
/// 平台分区的节点实例行
/// </summary>
/// <remarks>
/// 平台实例的节点实例，落平台库。索引：<c>(InstanceId)</c>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
[EntityIndex(nameof(InstanceId))]
public sealed class PlatformWorkflowNodeInstanceRecord : WorkflowNodeInstanceRow
{
}
