#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ActivityExecutionContext
// Guid:29e4a8d0-6f13-4c57-b8a2-04d9c1e57f36
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:22:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 活动执行上下文
/// </summary>
public class ActivityExecutionContext
{
    /// <summary>
    /// 流程定义
    /// </summary>
    public required WorkflowDefinition Definition { get; init; }

    /// <summary>
    /// 流程实例
    /// </summary>
    public required WorkflowInstance Instance { get; init; }

    /// <summary>
    /// 当前节点定义
    /// </summary>
    public required WorkflowNode Node { get; init; }

    /// <summary>
    /// 当前节点实例
    /// </summary>
    public required WorkflowNodeInstance NodeInstance { get; init; }

    /// <summary>
    /// 实例变量容器
    /// </summary>
    public required WorkflowVariables Variables { get; init; }

    /// <summary>
    /// 服务提供者（活动内解析依赖）
    /// </summary>
    public required IServiceProvider ServiceProvider { get; init; }

    /// <summary>
    /// 取消令牌
    /// </summary>
    public CancellationToken CancellationToken { get; init; }
}
