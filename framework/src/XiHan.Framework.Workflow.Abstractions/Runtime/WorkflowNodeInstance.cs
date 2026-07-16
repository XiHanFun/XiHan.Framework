#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowNodeInstance
// Guid:61e8b2c9-4d07-4fa5-b391-8c25f60d7a14
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:15:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.Abstractions.Runtime;

/// <summary>
/// 节点实例（一次节点执行的运行记录）
/// </summary>
public class WorkflowNodeInstance
{
    /// <summary>
    /// 节点实例标识
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 所属流程实例标识
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 节点标识
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// 节点名称快照
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 活动类型编码
    /// </summary>
    public string ActivityType { get; set; } = string.Empty;

    /// <summary>
    /// 状态
    /// </summary>
    public WorkflowNodeInstanceStatus Status { get; set; } = WorkflowNodeInstanceStatus.Running;

    /// <summary>
    /// 尝试次数（含首次执行）
    /// </summary>
    public int TryCount { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 输入快照（挂起恢复时为恢复输入）
    /// </summary>
    public Dictionary<string, object?> Inputs { get; set; } = [];

    /// <summary>
    /// 输出快照（完成时同时合并进实例变量）
    /// </summary>
    public Dictionary<string, object?> Outputs { get; set; } = [];

    /// <summary>
    /// 活动私有状态（会签进度、遍历游标等，跨挂起恢复保持）
    /// </summary>
    public Dictionary<string, object?> State { get; set; } = [];

    /// <summary>
    /// 故障信息
    /// </summary>
    public string? FaultMessage { get; set; }

    /// <summary>
    /// 补偿时间
    /// </summary>
    public DateTime? CompensatedTime { get; set; }

    /// <summary>
    /// 租户标识
    /// </summary>
    public long? TenantId { get; set; }
}
