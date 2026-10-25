// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 完成一个用户任务：操作者必须是任务的指派人。
/// </summary>
/// <param name="TaskId">任务 ID。</param>
/// <param name="ActorId">操作者。</param>
/// <param name="Outcome">结果（须在节点允许范围内）。</param>
/// <param name="Comment">意见。</param>
/// <param name="Variables">随任务写入实例的变量。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record UserTaskCompletion(string TaskId, string ActorId, string Outcome, string? Comment = null, JsonObject? Variables = null);
