// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 给一个用户任务加签：<c>Any</c> / <c>All</c> 立即新建任务，<c>Sequential</c> 排到队尾。
/// </summary>
/// <param name="TaskId">任务 ID。</param>
/// <param name="ActorId">操作者（当前指派人）。</param>
/// <param name="AssigneeIds">新增的指派人。</param>
/// <param name="Comment">意见。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record UserTaskAddition(string TaskId, string ActorId, IReadOnlyList<string> AssigneeIds, string? Comment = null);
