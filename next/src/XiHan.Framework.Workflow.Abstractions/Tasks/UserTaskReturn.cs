// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 退回请求：从本任务所在节点跳回上游节点。
/// </summary>
/// <param name="TaskId">任务 ID。</param>
/// <param name="ActorId">操作者（必须是指派人）。</param>
/// <param name="TargetNodeId">目标节点；空为本节点之前最近完成的 <c>UserTask</c> 节点，没有则 <c>Start</c> 之后的首个节点。</param>
/// <param name="Comment">意见（写进节点历史与事件）。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record UserTaskReturn(string TaskId, string ActorId, string? TargetNodeId = null, string? Comment = null);
