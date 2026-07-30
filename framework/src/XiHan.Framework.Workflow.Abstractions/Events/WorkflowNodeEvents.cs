// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
