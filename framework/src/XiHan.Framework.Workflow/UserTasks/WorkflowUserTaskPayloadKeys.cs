#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowUserTaskPayloadKeys
// Guid:37a5d9e2-14cf-4b80-96d3-e70b28c51f49
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:57:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.UserTasks;

/// <summary>
/// 人工任务书签载荷键常量
/// </summary>
public static class WorkflowUserTaskPayloadKeys
{
    /// <summary>
    /// 任务标题
    /// </summary>
    public const string Title = "title";

    /// <summary>
    /// 表单数据
    /// </summary>
    public const string FormData = "formData";

    /// <summary>
    /// 抄送人标识集合
    /// </summary>
    public const string CcUserIds = "ccUserIds";
}

/// <summary>
/// 人工任务恢复输入键常量
/// </summary>
public static class WorkflowUserTaskInputKeys
{
    /// <summary>
    /// 办理人标识
    /// </summary>
    public const string ActorId = "actorId";

    /// <summary>
    /// 办理结果
    /// </summary>
    public const string Outcome = "outcome";

    /// <summary>
    /// 办理意见
    /// </summary>
    public const string Comment = "comment";
}
