#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowUserTask
// Guid:cb92e6f0-1a75-4d38-9c04-e83b57d21f66
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:37:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.Abstractions.UserTasks;

/// <summary>
/// 人工待办任务（人工任务书签的业务视图）
/// </summary>
public class WorkflowUserTask
{
    /// <summary>
    /// 任务标识（即书签标识）
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// 流程实例标识
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 实例名称
    /// </summary>
    public string InstanceName { get; set; } = string.Empty;

    /// <summary>
    /// 定义编码
    /// </summary>
    public string DefinitionCode { get; set; } = string.Empty;

    /// <summary>
    /// 节点标识
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// 节点实例标识
    /// </summary>
    public string NodeInstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 任务标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 受理人标识
    /// </summary>
    public string AssigneeId { get; set; } = string.Empty;

    /// <summary>
    /// 业务相关性标识
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// 表单数据（人工任务节点声明的附加数据）
    /// </summary>
    public Dictionary<string, object?> FormData { get; set; } = [];

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreationTime { get; set; }

    /// <summary>
    /// 租户标识
    /// </summary>
    public long? TenantId { get; set; }
}
