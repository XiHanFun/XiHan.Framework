// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions.Engine;

/// <summary>
/// 波次队列里的一项工作：新执行、恢复或重试某个节点。
/// </summary>
/// <param name="Kind">种类。</param>
/// <param name="Node">节点。</param>
/// <param name="NodeInstance">恢复或重试时复用的节点实例。</param>
/// <param name="Bookmark">恢复时被消费的书签。</param>
/// <param name="Inputs">恢复输入。</param>
internal sealed record WorkItem(WorkItemKind Kind, WorkflowNode Node, WorkflowNodeInstance? NodeInstance = null, WorkflowBookmark? Bookmark = null, JsonObject? Inputs = null);
