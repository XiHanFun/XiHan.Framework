// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.ExternalTasks;

/// <summary>
/// 外部任务完成。
/// </summary>
/// <param name="TaskId">任务 ID。</param>
/// <param name="WorkerId">工作者（必须持有锁）。</param>
/// <param name="Outputs">输出，合并进实例变量。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ExternalTaskCompletion(string TaskId, string WorkerId, JsonObject? Outputs = null);
