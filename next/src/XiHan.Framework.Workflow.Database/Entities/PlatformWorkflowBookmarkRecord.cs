// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Database.Entities;

/// <summary>
/// 平台分区的书签行。
/// </summary>
/// <remarks>
/// 平台实例与平台定义的书签，落平台库。索引：<c>(InstanceId)</c>、<c>(Kind, Key)</c>、<c>(DueAt)</c>，用途同 <see cref="WorkflowBookmarkRecord"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
[EntityIndex(nameof(InstanceId))]
[EntityIndex(nameof(Kind), nameof(Key))]
[EntityIndex(nameof(DueAt))]
public sealed class PlatformWorkflowBookmarkRecord : WorkflowBookmarkRow
{
}
