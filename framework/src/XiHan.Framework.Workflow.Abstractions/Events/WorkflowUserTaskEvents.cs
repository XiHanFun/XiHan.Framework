#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowUserTaskEvents
// Guid:e17d6b93-0f42-4c85-a2d6-91b30c57f8e0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:41:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Workflow.Abstractions.UserTasks;

namespace XiHan.Framework.Workflow.Abstractions.Events;

/// <summary>
/// 人工任务已创建事件（应用侧订阅后向受理人与抄送人发送通知）
/// </summary>
/// <param name="Task">待办任务</param>
/// <param name="CcUserIds">抄送人标识集合</param>
public sealed record WorkflowUserTaskCreatedEventData(WorkflowUserTask Task, List<string> CcUserIds);

/// <summary>
/// 人工任务已办理事件
/// </summary>
/// <param name="TaskId">任务标识</param>
/// <param name="InstanceId">流程实例标识</param>
/// <param name="NodeId">节点标识</param>
/// <param name="ActorId">办理人标识</param>
/// <param name="Outcome">办理结果</param>
/// <param name="Comment">办理意见</param>
public sealed record WorkflowUserTaskCompletedEventData(
    string TaskId,
    string InstanceId,
    string NodeId,
    string ActorId,
    string Outcome,
    string? Comment);

/// <summary>
/// 人工任务已转办事件
/// </summary>
/// <param name="TaskId">任务标识</param>
/// <param name="InstanceId">流程实例标识</param>
/// <param name="ActorId">操作人标识</param>
/// <param name="TargetAssigneeId">新受理人标识</param>
/// <param name="Comment">转办意见</param>
public sealed record WorkflowUserTaskTransferredEventData(
    string TaskId,
    string InstanceId,
    string ActorId,
    string TargetAssigneeId,
    string? Comment);
