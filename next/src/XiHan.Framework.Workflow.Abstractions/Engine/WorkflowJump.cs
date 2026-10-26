// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Engine;

/// <summary>
/// 跳转请求：取消未结束的节点与书签后从目标节点重新执行。
/// </summary>
/// <param name="InstanceId">实例 ID。</param>
/// <param name="TargetNodeId">目标节点 ID。</param>
/// <param name="Reason">原因（写进事件）。</param>
/// <param name="Variables">跳转前合并进实例的变量。</param>
/// <param name="ActorId">操作者（写进事件）。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowJump(string InstanceId, string TargetNodeId, string? Reason = null, JsonObject? Variables = null, string? ActorId = null);
