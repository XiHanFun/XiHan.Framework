// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 转办一个用户任务：把任务交给另一个指派人。
/// </summary>
/// <param name="TaskId">任务 ID。</param>
/// <param name="ActorId">操作者（当前指派人）。</param>
/// <param name="TargetAssigneeId">目标指派人。</param>
/// <param name="Comment">意见。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record UserTaskTransfer(string TaskId, string ActorId, string TargetAssigneeId, string? Comment = null);
