// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Expressions;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 从书签恢复时的上下文：多了被消费的书签与恢复输入。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ActivityResumeContext : ActivityContext
{
    /// <summary>
    /// 由引擎创建。
    /// </summary>
    internal ActivityResumeContext(WorkflowDefinition definition, WorkflowInstance instance, WorkflowNode node, WorkflowNodeInstance nodeInstance, WorkflowVariables variables, IServiceProvider services, InvocationContext invocation, IWorkflowExpressionEvaluator evaluator, TimeProvider clock, WorkflowBookmark bookmark, JsonObject inputs)
        : base(definition, instance, node, nodeInstance, variables, services, invocation, evaluator, clock)
    {
        Bookmark = bookmark;
        Inputs = inputs;
    }

    /// <summary>
    /// 被消费的书签。
    /// </summary>
    public WorkflowBookmark Bookmark { get; }

    /// <summary>
    /// 恢复输入（信号载荷、任务完成输入、子流程回调等）。
    /// </summary>
    public JsonObject Inputs { get; }

    /// <summary>
    /// 是否因节点超时恢复。
    /// </summary>
    public bool IsTimeout => string.Equals(Bookmark.Kind, BookmarkKinds.NodeTimeout, StringComparison.Ordinal);
}
