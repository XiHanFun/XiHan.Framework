// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Engine;

/// <summary>
/// 改实例变量的请求。
/// </summary>
/// <param name="InstanceId">实例 ID。</param>
/// <param name="Variables">要合并（新增或覆盖）的变量。</param>
/// <param name="Remove">要删除的变量名。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowVariableUpdate(string InstanceId, JsonObject? Variables, IReadOnlyList<string>? Remove = null);
