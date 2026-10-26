// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 释放认领的请求。
/// </summary>
/// <param name="TaskId">任务 ID。</param>
/// <param name="ActorId">操作者（必须是认领人）。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record UserTaskRelease(string TaskId, string ActorId);
