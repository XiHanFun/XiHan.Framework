// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Definitions;

namespace XiHan.Framework.Workflow.Builders;

/// <summary>
/// 流程节点构建器
/// </summary>
public class WorkflowNodeBuilder
{
    private readonly WorkflowNode _node;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="node">被构建的节点</param>
    public WorkflowNodeBuilder(WorkflowNode node)
    {
        _node = node;
    }

    /// <summary>
    /// 设置节点名称
    /// </summary>
    /// <param name="name">节点名称</param>
    /// <returns>节点构建器</returns>
    public WorkflowNodeBuilder WithName(string name)
    {
        _node.Name = name;
        return this;
    }

    /// <summary>
    /// 设置活动属性
    /// </summary>
    /// <param name="name">属性名</param>
    /// <param name="value">属性值</param>
    /// <returns>节点构建器</returns>
    public WorkflowNodeBuilder WithProperty(string name, object? value)
    {
        _node.Properties[name] = value;
        return this;
    }

    /// <summary>
    /// 批量设置活动属性
    /// </summary>
    /// <param name="properties">属性字典</param>
    /// <returns>节点构建器</returns>
    public WorkflowNodeBuilder WithProperties(IEnumerable<KeyValuePair<string, object?>> properties)
    {
        foreach (var pair in properties)
        {
            _node.Properties[pair.Key] = pair.Value;
        }

        return this;
    }

    /// <summary>
    /// 设置重试策略
    /// </summary>
    /// <param name="maxAttempts">最大尝试次数（含首次执行）</param>
    /// <param name="firstDelaySeconds">首次重试等待秒数</param>
    /// <param name="backoffFactor">退避倍率</param>
    /// <returns>节点构建器</returns>
    public WorkflowNodeBuilder WithRetry(int maxAttempts, int firstDelaySeconds = 10, double backoffFactor = 2.0)
    {
        _node.RetryPolicy = new WorkflowRetryPolicy
        {
            MaxAttempts = maxAttempts,
            FirstDelaySeconds = firstDelaySeconds,
            BackoffFactor = backoffFactor
        };
        return this;
    }

    /// <summary>
    /// 设置节点挂起超时
    /// </summary>
    /// <param name="timeoutSeconds">超时秒数</param>
    /// <returns>节点构建器</returns>
    public WorkflowNodeBuilder WithTimeout(int timeoutSeconds)
    {
        _node.TimeoutSeconds = timeoutSeconds;
        return this;
    }

    /// <summary>
    /// 启用失败续行
    /// </summary>
    /// <returns>节点构建器</returns>
    public WorkflowNodeBuilder WithContinueOnError()
    {
        _node.ContinueOnError = true;
        return this;
    }
}
