// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 认领请求（<c>Any</c> 策略下由一个指派人独占任务）。
/// </summary>
/// <param name="TaskId">任务 ID。</param>
/// <param name="ActorId">操作者（必须是指派人）。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record UserTaskClaim(string TaskId, string ActorId);
