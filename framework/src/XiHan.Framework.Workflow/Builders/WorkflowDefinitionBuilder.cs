// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Diagnostics;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Exceptions;

namespace XiHan.Framework.Workflow.Builders;

/// <summary>
/// 流程定义构建器（代码方式定义流程的流式入口）
/// </summary>
/// <remarks>
/// <code>
/// var definition = WorkflowDefinitionBuilder.Create("leave-request", "请假审批")
///     .AddVariable("amount", required: true)
///     .AddStart()
///     .AddDecision("gateway")
///     .AddUserTask("manager", "经理审批", node => node.WithProperty("Assignees", new List&lt;string&gt; { "1001" }))
///     .AddEnd()
///     .AddTransition("start", "gateway")
///     .AddTransition("gateway", "manager", "amount &gt; 10000")
///     .AddDefaultTransition("gateway", "end")
///     .AddTransition("manager", "end")
///     .Build();
/// </code>
/// </remarks>
public class WorkflowDefinitionBuilder
{
    private readonly WorkflowDefinition _definition;
    private int _transitionSequence;

    private WorkflowDefinitionBuilder(string code, string name)
    {
        _definition = new WorkflowDefinition
        {
            Code = code,
            Name = name
        };
    }

    /// <summary>
    /// 创建构建器
    /// </summary>
    /// <param name="code">流程编码</param>
    /// <param name="name">流程名称</param>
    /// <returns>流程定义构建器</returns>
    public static WorkflowDefinitionBuilder Create(string code, string name)
    {
        Guard.NotNullOrWhiteSpace(code, nameof(code));
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        return new WorkflowDefinitionBuilder(code, name);
    }

    /// <summary>
    /// 设置描述
    /// </summary>
    /// <param name="description">描述</param>
    /// <returns>流程定义构建器</returns>
    public WorkflowDefinitionBuilder WithDescription(string description)
    {
        _definition.Description = description;
        return this;
    }

    /// <summary>
    /// 设置分类
    /// </summary>
    /// <param name="category">分类</param>
    /// <returns>流程定义构建器</returns>
    public WorkflowDefinitionBuilder WithCategory(string category)
    {
        _definition.Category = category;
        return this;
    }

    /// <summary>
    /// 启用补偿
    /// </summary>
    /// <returns>流程定义构建器</returns>
    public WorkflowDefinitionBuilder WithCompensation()
    {
        _definition.EnableCompensation = true;
        return this;
    }

    /// <summary>
    /// 声明启动变量
    /// </summary>
    /// <param name="name">变量名</param>
    /// <param name="required">是否必填</param>
    /// <param name="defaultValue">默认值</param>
    /// <param name="description">描述</param>
    /// <returns>流程定义构建器</returns>
    public WorkflowDefinitionBuilder AddVariable(string name, bool required = false, object? defaultValue = null, string? description = null)
    {
        _definition.Variables.Add(new WorkflowVariableDefinition
        {
            Name = name,
            Required = required,
            DefaultValue = defaultValue,
            Description = description
        });
        return this;
    }

    /// <summary>
    /// 添加节点
    /// </summary>
    /// <param name="id">节点标识</param>
    /// <param name="activityType">活动类型编码</param>
    /// <param name="name">节点名称（默认取节点标识）</param>
    /// <param name="configure">节点配置委托</param>
    /// <returns>流程定义构建器</returns>
    public WorkflowDefinitionBuilder AddNode(string id, string activityType, string? name = null, Action<WorkflowNodeBuilder>? configure = null)
    {
        Guard.NotNullOrWhiteSpace(id, nameof(id));
        Guard.NotNullOrWhiteSpace(activityType, nameof(activityType));

        if (_definition.Nodes.Any(node => node.Id == id))
        {
            throw new WorkflowException($"节点标识 {id} 重复");
        }

        var node = new WorkflowNode
        {
            Id = id,
            Name = name ?? id,
            ActivityType = activityType
        };
        _definition.Nodes.Add(node);
        configure?.Invoke(new WorkflowNodeBuilder(node));
        return this;
    }

    /// <summary>
    /// 添加开始节点
    /// </summary>
    /// <param name="id">节点标识</param>
    /// <returns>流程定义构建器</returns>
    public WorkflowDefinitionBuilder AddStart(string id = "start")
    {
        return AddNode(id, WorkflowActivityTypes.Start, "开始");
    }

    /// <summary>
    /// 添加结束节点
    /// </summary>
    /// <param name="id">节点标识</param>
    /// <returns>流程定义构建器</returns>
    public WorkflowDefinitionBuilder AddEnd(string id = "end")
    {
        return AddNode(id, WorkflowActivityTypes.End, "结束");
    }

    /// <summary>
    /// 添加独占网关节点
    /// </summary>
    /// <param name="id">节点标识</param>
    /// <param name="name">节点名称</param>
    /// <returns>流程定义构建器</returns>
    public WorkflowDefinitionBuilder AddDecision(string id, string? name = null)
    {
        return AddNode(id, WorkflowActivityTypes.Decision, name);
    }

    /// <summary>
    /// 添加并行网关节点
    /// </summary>
    /// <param name="id">节点标识</param>
    /// <param name="name">节点名称</param>
    /// <returns>流程定义构建器</returns>
    public WorkflowDefinitionBuilder AddParallel(string id, string? name = null)
    {
        return AddNode(id, WorkflowActivityTypes.Parallel, name);
    }

    /// <summary>
    /// 添加汇聚网关节点
    /// </summary>
    /// <param name="id">节点标识</param>
    /// <param name="waitAny">是否任一到达即触发（默认等待全部）</param>
    /// <param name="name">节点名称</param>
    /// <returns>流程定义构建器</returns>
    public WorkflowDefinitionBuilder AddJoin(string id, bool waitAny = false, string? name = null)
    {
        return AddNode(id, WorkflowActivityTypes.Join, name,
            node => node.WithProperty("Mode", waitAny ? "WaitAny" : "WaitAll"));
    }

    /// <summary>
    /// 添加延时节点
    /// </summary>
    /// <param name="id">节点标识</param>
    /// <param name="durationSeconds">等待秒数</param>
    /// <param name="name">节点名称</param>
    /// <returns>流程定义构建器</returns>
    public WorkflowDefinitionBuilder AddDelay(string id, double durationSeconds, string? name = null)
    {
        return AddNode(id, WorkflowActivityTypes.Delay, name,
            node => node.WithProperty("DurationSeconds", durationSeconds));
    }

    /// <summary>
    /// 添加人工任务节点
    /// </summary>
    /// <param name="id">节点标识</param>
    /// <param name="name">节点名称</param>
    /// <param name="configure">节点配置委托</param>
    /// <returns>流程定义构建器</returns>
    public WorkflowDefinitionBuilder AddUserTask(string id, string name, Action<WorkflowNodeBuilder> configure)
    {
        return AddNode(id, WorkflowActivityTypes.UserTask, name, configure);
    }

    /// <summary>
    /// 添加连线
    /// </summary>
    /// <param name="sourceNodeId">源节点标识</param>
    /// <param name="targetNodeId">目标节点标识</param>
    /// <param name="condition">转移条件表达式（为空表示无条件）</param>
    /// <param name="priority">优先级（独占网关按升序求值）</param>
    /// <param name="name">连线名称</param>
    /// <returns>流程定义构建器</returns>
    public WorkflowDefinitionBuilder AddTransition(
        string sourceNodeId,
        string targetNodeId,
        string? condition = null,
        int priority = 0,
        string? name = null)
    {
        _definition.Transitions.Add(new WorkflowTransition
        {
            Id = $"{sourceNodeId}->{targetNodeId}#{++_transitionSequence}",
            Name = name,
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            Condition = condition,
            Priority = priority
        });
        return this;
    }

    /// <summary>
    /// 添加默认连线（独占网关兜底分支）
    /// </summary>
    /// <param name="sourceNodeId">源节点标识</param>
    /// <param name="targetNodeId">目标节点标识</param>
    /// <param name="name">连线名称</param>
    /// <returns>流程定义构建器</returns>
    public WorkflowDefinitionBuilder AddDefaultTransition(string sourceNodeId, string targetNodeId, string? name = null)
    {
        _definition.Transitions.Add(new WorkflowTransition
        {
            Id = $"{sourceNodeId}->{targetNodeId}#{++_transitionSequence}",
            Name = name,
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            IsDefault = true,
            Priority = int.MaxValue
        });
        return this;
    }

    /// <summary>
    /// 构建流程定义（引用完整性由发布时的校验器把关）
    /// </summary>
    /// <returns>流程定义</returns>
    public WorkflowDefinition Build()
    {
        return _definition;
    }
}
