// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 催办请求：发布 <c>workflow.task.urged</c> 事件。
/// </summary>
/// <param name="TaskId">任务 ID。</param>
/// <param name="ActorId">催办人。</param>
/// <param name="Comment">留言。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record UserTaskUrge(string TaskId, string ActorId, string? Comment = null);
