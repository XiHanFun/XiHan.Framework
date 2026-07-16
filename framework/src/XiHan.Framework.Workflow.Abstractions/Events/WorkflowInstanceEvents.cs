#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowInstanceEvents
// Guid:df30b8a6-27c1-4e59-b4f7-08a52d61c9e3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:39:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Events;

/// <summary>
/// 流程实例已启动事件
/// </summary>
/// <param name="Instance">流程实例</param>
public sealed record WorkflowInstanceStartedEventData(WorkflowInstance Instance);

/// <summary>
/// 流程实例已完成事件
/// </summary>
/// <param name="Instance">流程实例</param>
public sealed record WorkflowInstanceCompletedEventData(WorkflowInstance Instance);

/// <summary>
/// 流程实例已故障事件
/// </summary>
/// <param name="Instance">流程实例</param>
public sealed record WorkflowInstanceFaultedEventData(WorkflowInstance Instance);

/// <summary>
/// 流程实例已取消事件
/// </summary>
/// <param name="Instance">流程实例</param>
public sealed record WorkflowInstanceCanceledEventData(WorkflowInstance Instance);

/// <summary>
/// 流程实例已终止事件
/// </summary>
/// <param name="Instance">流程实例</param>
public sealed record WorkflowInstanceTerminatedEventData(WorkflowInstance Instance);

/// <summary>
/// 流程实例已挂起事件
/// </summary>
/// <param name="Instance">流程实例</param>
/// <param name="Reason">挂起原因</param>
public sealed record WorkflowInstanceSuspendedEventData(WorkflowInstance Instance, string? Reason);

/// <summary>
/// 流程实例已恢复运行事件
/// </summary>
/// <param name="Instance">流程实例</param>
public sealed record WorkflowInstanceResumedEventData(WorkflowInstance Instance);

/// <summary>
/// 流程自定义业务事件（发布事件活动产生）
/// </summary>
/// <param name="EventName">事件名称</param>
/// <param name="InstanceId">流程实例标识</param>
/// <param name="CorrelationId">业务相关性标识</param>
/// <param name="Payload">事件载荷</param>
public sealed record WorkflowCustomEventData(
    string EventName,
    string InstanceId,
    string? CorrelationId,
    Dictionary<string, object?> Payload);
