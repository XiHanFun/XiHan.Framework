// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Tests.Fakes;

/// <summary>
/// 测试用最小服务提供者
/// </summary>
/// <remarks>
/// 抽象层自身不解析任何依赖，活动上下文只要求 <see cref="IServiceProvider"/> 非空，
/// 手写恒返回 null 的实现即可，避免为构造上下文引入真实容器。
/// </remarks>
internal sealed class FakeServiceProvider : IServiceProvider
{
    /// <summary>
    /// 获取服务（恒返回 null）
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <returns>恒为 null</returns>
    public object? GetService(Type serviceType)
    {
        return null;
    }
}

/// <summary>
/// 抽象层测试的公共模型构造工具
/// </summary>
internal static class WorkflowTestModels
{
    /// <summary>
    /// 构造一条最小可用的两节点流程定义
    /// </summary>
    /// <returns>流程定义</returns>
    public static WorkflowDefinition CreateDefinition()
    {
        return new WorkflowDefinition
        {
            Id = "def-1",
            Code = "leave",
            Name = "请假流程",
            Version = 1,
            Nodes =
            [
                new WorkflowNode { Id = "start", Name = "开始", ActivityType = WorkflowActivityTypes.Start },
                new WorkflowNode { Id = "end", Name = "结束", ActivityType = WorkflowActivityTypes.End }
            ],
            Transitions =
            [
                new WorkflowTransition { Id = "t1", SourceNodeId = "start", TargetNodeId = "end" }
            ]
        };
    }

    /// <summary>
    /// 构造一个运行中的流程实例
    /// </summary>
    /// <returns>流程实例</returns>
    public static WorkflowInstance CreateInstance()
    {
        return new WorkflowInstance
        {
            Id = "ins-1",
            DefinitionId = "def-1",
            DefinitionCode = "leave",
            DefinitionVersion = 1,
            Name = "请假流程"
        };
    }

    /// <summary>
    /// 构造一个运行中的节点实例
    /// </summary>
    /// <returns>节点实例</returns>
    public static WorkflowNodeInstance CreateNodeInstance()
    {
        return new WorkflowNodeInstance
        {
            Id = "ni-1",
            InstanceId = "ins-1",
            NodeId = "start",
            Name = "开始",
            ActivityType = WorkflowActivityTypes.Start
        };
    }

    /// <summary>
    /// 构造活动执行上下文
    /// </summary>
    /// <param name="variables">实例变量字典（为空时使用实例自带的空字典）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>活动执行上下文</returns>
    public static ActivityExecutionContext CreateExecutionContext(
        Dictionary<string, object?>? variables = null,
        CancellationToken cancellationToken = default)
    {
        var definition = CreateDefinition();
        var instance = CreateInstance();
        if (variables is not null)
        {
            instance.Variables = variables;
        }

        return new ActivityExecutionContext
        {
            Definition = definition,
            Instance = instance,
            Node = definition.Nodes[0],
            NodeInstance = CreateNodeInstance(),
            Variables = new WorkflowVariables(instance.Variables),
            ServiceProvider = new FakeServiceProvider(),
            CancellationToken = cancellationToken
        };
    }

    /// <summary>
    /// 构造活动恢复上下文
    /// </summary>
    /// <param name="inputs">恢复输入（为空时使用默认空字典）</param>
    /// <returns>活动恢复上下文</returns>
    public static ActivityResumeContext CreateResumeContext(Dictionary<string, object?>? inputs = null)
    {
        var definition = CreateDefinition();
        var instance = CreateInstance();
        var bookmark = new WorkflowBookmark
        {
            Id = "bm-1",
            InstanceId = instance.Id,
            NodeId = "start",
            NodeInstanceId = "ni-1",
            Kind = WorkflowBookmarkKinds.UserTask,
            Key = "u-1"
        };

        return inputs is null
            ? new ActivityResumeContext
            {
                Definition = definition,
                Instance = instance,
                Node = definition.Nodes[0],
                NodeInstance = CreateNodeInstance(),
                Variables = new WorkflowVariables(instance.Variables),
                ServiceProvider = new FakeServiceProvider(),
                Bookmark = bookmark
            }
            : new ActivityResumeContext
            {
                Definition = definition,
                Instance = instance,
                Node = definition.Nodes[0],
                NodeInstance = CreateNodeInstance(),
                Variables = new WorkflowVariables(instance.Variables),
                ServiceProvider = new FakeServiceProvider(),
                Bookmark = bookmark,
                Inputs = inputs
            };
    }
}
