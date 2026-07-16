#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowInstance
// Guid:d94a07f6-15b3-4c82-9e60-4f7c28a1b5d9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:14:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.Abstractions.Runtime;

/// <summary>
/// 流程实例
/// </summary>
public class WorkflowInstance
{
    /// <summary>
    /// 实例标识
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 定义标识
    /// </summary>
    public string DefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// 定义编码
    /// </summary>
    public string DefinitionCode { get; set; } = string.Empty;

    /// <summary>
    /// 定义版本
    /// </summary>
    public int DefinitionVersion { get; set; }

    /// <summary>
    /// 实例名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 状态
    /// </summary>
    public WorkflowInstanceStatus Status { get; set; } = WorkflowInstanceStatus.Running;

    /// <summary>
    /// 实例变量
    /// </summary>
    public Dictionary<string, object?> Variables { get; set; } = [];

    /// <summary>
    /// 汇聚网关波次状态（键 = 汇聚节点标识）
    /// </summary>
    public Dictionary<string, WorkflowJoinState> JoinStates { get; set; } = [];

    /// <summary>
    /// 业务相关性标识（外部单据号等，用于信号匹配与业务查询）
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// 发起人标识
    /// </summary>
    public string? StarterId { get; set; }

    /// <summary>
    /// 父实例标识（子流程场景）
    /// </summary>
    public string? ParentInstanceId { get; set; }

    /// <summary>
    /// 父节点实例标识（子流程场景，终态时用于回调父实例书签）
    /// </summary>
    public string? ParentNodeInstanceId { get; set; }

    /// <summary>
    /// 实例深度（顶层为 0，子流程逐层加一）
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// 租户标识
    /// </summary>
    public long? TenantId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreationTime { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间（进入终态时间；故障实例重试后清空）
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 故障信息
    /// </summary>
    public string? FaultMessage { get; set; }

    /// <summary>
    /// 故障节点标识
    /// </summary>
    public string? FaultNodeId { get; set; }

    /// <summary>
    /// 故障节点实例标识（重试入口）
    /// </summary>
    public string? FaultNodeInstanceId { get; set; }

    /// <summary>
    /// 取消/终止原因
    /// </summary>
    public string? CancellationReason { get; set; }

    /// <summary>
    /// 是否处于终态（完成/取消/故障/终止）
    /// </summary>
    /// <returns>处于终态返回 true</returns>
    public bool IsFinalStatus()
    {
        return Status is WorkflowInstanceStatus.Completed
            or WorkflowInstanceStatus.Canceled
            or WorkflowInstanceStatus.Faulted
            or WorkflowInstanceStatus.Terminated;
    }
}
