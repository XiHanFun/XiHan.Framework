#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowNodeEvents
// Guid:4a95c1f7-8d06-4b23-9e51-6c7f30d28ab4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:40:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Events;

/// <summary>
/// 节点开始执行事件
/// </summary>
/// <param name="Instance">流程实例</param>
/// <param name="NodeInstance">节点实例</param>
public sealed record WorkflowNodeExecutingEventData(WorkflowInstance Instance, WorkflowNodeInstance NodeInstance);

/// <summary>
/// 节点执行完成事件（含完成与挂起）
/// </summary>
/// <param name="Instance">流程实例</param>
/// <param name="NodeInstance">节点实例</param>
public sealed record WorkflowNodeExecutedEventData(WorkflowInstance Instance, WorkflowNodeInstance NodeInstance);

/// <summary>
/// 节点故障事件
/// </summary>
/// <param name="Instance">流程实例</param>
/// <param name="NodeInstance">节点实例</param>
/// <param name="WillRetry">是否已排期重试</param>
public sealed record WorkflowNodeFaultedEventData(WorkflowInstance Instance, WorkflowNodeInstance NodeInstance, bool WillRetry);
