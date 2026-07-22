// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
