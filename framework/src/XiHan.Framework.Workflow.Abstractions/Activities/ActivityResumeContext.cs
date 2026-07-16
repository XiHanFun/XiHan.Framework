#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ActivityResumeContext
// Guid:7b06d5f2-3c98-4ea1-a75d-8f214c60b9e3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:23:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
