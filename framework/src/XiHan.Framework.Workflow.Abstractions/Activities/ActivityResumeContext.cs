// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 活动恢复上下文（书签恢复时使用）
/// </summary>
public class ActivityResumeContext : ActivityExecutionContext
{
    /// <summary>
    /// 被消费的书签
    /// </summary>
    public required WorkflowBookmark Bookmark { get; init; }

    /// <summary>
    /// 恢复输入（审批意见、信号载荷、子流程结果等）
    /// </summary>
    public Dictionary<string, object?> Inputs { get; init; } = [];
}
