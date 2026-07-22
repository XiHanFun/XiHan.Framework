// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.Abstractions.Runtime;

/// <summary>
/// 流程书签（挂起节点的可恢复等待点）
/// </summary>
public class WorkflowBookmark
{
    /// <summary>
    /// 书签标识
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
    /// 节点实例标识
    /// </summary>
    public string NodeInstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 书签种类（见 <see cref="WorkflowBookmarkKinds"/>）
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 索引键（语义随种类而定：受理人标识/信号名称/父节点实例标识）
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// 附加数据（任务标题、表单数据等）
    /// </summary>
    public Dictionary<string, object?> Payload { get; set; } = [];

    /// <summary>
    /// 到期时间（定时类书签由定时器 Worker 轮询到期恢复）
    /// </summary>
    public DateTime? DueTime { get; set; }

    /// <summary>
    /// 业务相关性标识（信号定向匹配）
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreationTime { get; set; }

    /// <summary>
    /// 租户标识
    /// </summary>
    public long? TenantId { get; set; }
}
