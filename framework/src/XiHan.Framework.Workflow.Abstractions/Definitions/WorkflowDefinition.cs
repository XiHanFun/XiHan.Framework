// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 流程定义（某编码某版本的不可变模板，实例永远绑定具体版本）
/// </summary>
public class WorkflowDefinition
{
    /// <summary>
    /// 定义标识
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 流程编码（同编码可存在多个版本）
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 流程名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 版本号（同编码内自增，从 1 开始）
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 分类
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public WorkflowDefinitionStatus Status { get; set; } = WorkflowDefinitionStatus.Draft;

    /// <summary>
    /// 是否启用补偿（实例被取消时按执行逆序执行可补偿活动的补偿逻辑）
    /// </summary>
    public bool EnableCompensation { get; set; }

    /// <summary>
    /// 节点集合
    /// </summary>
    public List<WorkflowNode> Nodes { get; set; } = [];

    /// <summary>
    /// 连线集合
    /// </summary>
    public List<WorkflowTransition> Transitions { get; set; } = [];

    /// <summary>
    /// 启动变量声明
    /// </summary>
    public List<WorkflowVariableDefinition> Variables { get; set; } = [];

    /// <summary>
    /// 租户标识（为空表示平台级定义）
    /// </summary>
    public long? TenantId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreationTime { get; set; }

    /// <summary>
    /// 发布时间
    /// </summary>
    public DateTime? PublishTime { get; set; }

    /// <summary>
    /// 扩展属性（设计器画布布局等附加数据）
    /// </summary>
    public Dictionary<string, string> ExtraProperties { get; set; } = [];
}
