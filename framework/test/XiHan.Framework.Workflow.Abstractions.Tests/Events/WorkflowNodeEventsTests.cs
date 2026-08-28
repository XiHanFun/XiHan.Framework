// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Abstractions.Tests.Fakes;

namespace XiHan.Framework.Workflow.Abstractions.Tests.Events;

/// <summary>
/// 节点事件契约测试
/// </summary>
/// <remarks>
/// 节点故障事件的第三个位置参数 <c>WillRetry</c> 决定订阅方是否要告警：
/// 已排期重试的故障属于正常波动，不该触发人工介入，因此该标志的位置与语义必须锁死。
/// </remarks>
public class WorkflowNodeEventsTests
{
    /// <summary>
    /// 节点开始与完成事件按（实例, 节点实例）两个位置参数暴露
    /// </summary>
    [Fact]
    public void ExecutingAndExecutedEvents_ExposeInstanceAndNodeInstance()
    {
        var instance = WorkflowTestModels.CreateInstance();
        var nodeInstance = WorkflowTestModels.CreateNodeInstance();

        var executing = new WorkflowNodeExecutingEventData(instance, nodeInstance);
        var executed = new WorkflowNodeExecutedEventData(instance, nodeInstance);

        var (executingInstance, executingNode) = executing;
        var (executedInstance, executedNode) = executed;

        Assert.Same(instance, executingInstance);
        Assert.Same(nodeInstance, executingNode);
        Assert.Same(instance, executedInstance);
        Assert.Same(nodeInstance, executedNode);
    }

    /// <summary>
    /// 开始与完成事件是两个不同类型，不能互相顶替
    /// </summary>
    [Fact]
    public void ExecutingAndExecutedEvents_AreDistinctSealedTypes()
    {
        Assert.True(typeof(WorkflowNodeExecutingEventData).IsSealed);
        Assert.True(typeof(WorkflowNodeExecutedEventData).IsSealed);
        Assert.True(typeof(WorkflowNodeFaultedEventData).IsSealed);
        Assert.NotEqual(typeof(WorkflowNodeExecutingEventData), typeof(WorkflowNodeExecutedEventData));
    }

    /// <summary>
    /// 节点故障事件携带是否已排期重试的标志
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FaultedEvent_CarriesWillRetryFlag(bool willRetry)
    {
        var instance = WorkflowTestModels.CreateInstance();
        var nodeInstance = WorkflowTestModels.CreateNodeInstance();
        nodeInstance.Status = WorkflowNodeInstanceStatus.Faulted;
        nodeInstance.FaultMessage = "远端超时";

        var data = new WorkflowNodeFaultedEventData(instance, nodeInstance, willRetry);
        var (eventInstance, eventNodeInstance, flag) = data;

        Assert.Same(instance, eventInstance);
        Assert.Same(nodeInstance, eventNodeInstance);
        Assert.Equal(willRetry, flag);
        Assert.Equal(willRetry, data.WillRetry);
        Assert.Equal(WorkflowNodeInstanceStatus.Faulted, data.NodeInstance.Status);
        Assert.Equal("远端超时", data.NodeInstance.FaultMessage);
    }

    /// <summary>
    /// 仅重试标志不同的两条故障事件不相等
    /// </summary>
    [Fact]
    public void FaultedEvent_Equality_DistinguishesWillRetryFlag()
    {
        var instance = WorkflowTestModels.CreateInstance();
        var nodeInstance = WorkflowTestModels.CreateNodeInstance();

        var willRetry = new WorkflowNodeFaultedEventData(instance, nodeInstance, true);
        var wontRetry = new WorkflowNodeFaultedEventData(instance, nodeInstance, false);
        var sameAsWillRetry = new WorkflowNodeFaultedEventData(instance, nodeInstance, true);

        Assert.NotEqual(willRetry, wontRetry);
        Assert.Equal(willRetry, sameAsWillRetry);
    }
}
