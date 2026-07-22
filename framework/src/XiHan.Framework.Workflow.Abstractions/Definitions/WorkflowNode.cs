// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 流程节点
/// </summary>
public class WorkflowNode
{
    /// <summary>
    /// 节点标识（流程定义内唯一）
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 节点名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 活动类型编码（见 <see cref="WorkflowActivityTypes"/>，也可为自定义活动编码）
    /// </summary>
    public string ActivityType { get; set; } = string.Empty;

    /// <summary>
    /// 活动属性（各活动类型自行约定键名；字符串值支持 <c>{{ 表达式 }}</c> 模板插值）
    /// </summary>
    public Dictionary<string, object?> Properties { get; set; } = [];

    /// <summary>
    /// 重试策略（为空表示不重试）
    /// </summary>
    public WorkflowRetryPolicy? RetryPolicy { get; set; }

    /// <summary>
    /// 节点挂起超时秒数（为空表示不超时；仅对挂起型节点生效，超时后按超时语义恢复）
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// 失败续行（节点最终失败时不故障整个实例，写入 lastError 变量并以 error 结果继续流转）
    /// </summary>
    public bool ContinueOnError { get; set; }
}
