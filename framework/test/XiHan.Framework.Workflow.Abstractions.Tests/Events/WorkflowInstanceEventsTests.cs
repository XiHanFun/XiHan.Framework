// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 流程实例事件契约测试
/// </summary>
/// <remarks>
/// 这些事件是位置记录，订阅方会直接解构使用，因此参数顺序与相等性语义都是契约的一部分。
/// 尤其要注意：记录的合成相等性对引用类型成员用的是引用比较，
/// 两条内容相同但对象不同的事件并不相等，事件总线不能靠相等性去重。
/// </remarks>
public class WorkflowInstanceEventsTests
{
    /// <summary>
    /// 每类实例事件都是独立的封闭记录类型
    /// </summary>
    [Fact]
    public void EventTypes_AreSealedAndDistinct()
    {
        Assert.True(typeof(WorkflowInstanceStartedEventData).IsSealed);
        Assert.True(typeof(WorkflowInstanceCompletedEventData).IsSealed);
        Assert.True(typeof(WorkflowInstanceFaultedEventData).IsSealed);
        Assert.True(typeof(WorkflowInstanceCanceledEventData).IsSealed);
        Assert.True(typeof(WorkflowInstanceTerminatedEventData).IsSealed);
        Assert.True(typeof(WorkflowInstanceSuspendedEventData).IsSealed);
        Assert.True(typeof(WorkflowInstanceResumedEventData).IsSealed);
        Assert.True(typeof(WorkflowCustomEventData).IsSealed);

        Assert.NotEqual(typeof(WorkflowInstanceCompletedEventData), typeof(WorkflowInstanceCanceledEventData));
    }

    /// <summary>
    /// 单参事件携带实例引用且可解构
    /// </summary>
    [Fact]
    public void StartedEvent_ExposesInstanceAndSupportsDeconstruction()
    {
        var instance = WorkflowTestModels.CreateInstance();

        var data = new WorkflowInstanceStartedEventData(instance);
        data.Deconstruct(out var deconstructed);

        Assert.Same(instance, data.Instance);
        Assert.Same(instance, deconstructed);
    }

    /// <summary>
    /// 挂起事件携带可空的挂起原因
    /// </summary>
    [Fact]
    public void SuspendedEvent_CarriesOptionalReason()
    {
        var instance = WorkflowTestModels.CreateInstance();

        var withReason = new WorkflowInstanceSuspendedEventData(instance, "等待人工核查");
        var withoutReason = new WorkflowInstanceSuspendedEventData(instance, null);

        Assert.Equal("等待人工核查", withReason.Reason);
        Assert.Null(withoutReason.Reason);
        Assert.NotEqual(withReason, withoutReason);
    }

    /// <summary>
    /// 持有同一实例引用的两条事件相等
    /// </summary>
    [Fact]
    public void Equality_WithSameInstanceReference_IsEqual()
    {
        var instance = WorkflowTestModels.CreateInstance();

        var first = new WorkflowInstanceCompletedEventData(instance);
        var second = new WorkflowInstanceCompletedEventData(instance);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    /// <summary>
    /// 内容相同但实例对象不同的两条事件不相等
    /// </summary>
    /// <remarks>
    /// 记录的合成相等性对 <see cref="WorkflowInstance"/>（普通类，未重写 Equals）退化为引用比较，
    /// 订阅方不能假设"同一个实例的两条事件必然相等"，这条反直觉行为必须写进测试。
    /// </remarks>
    [Fact]
    public void Equality_WithEquivalentButDistinctInstances_IsNotEqual()
    {
        var first = new WorkflowInstanceCompletedEventData(WorkflowTestModels.CreateInstance());
        var second = new WorkflowInstanceCompletedEventData(WorkflowTestModels.CreateInstance());

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// 自定义业务事件按四个位置参数暴露事件名、实例、相关性与载荷
    /// </summary>
    [Fact]
    public void CustomEvent_ExposesFourPositionalMembers()
    {
        var payload = new Dictionary<string, object?> { ["amount"] = 2000 };

        var data = new WorkflowCustomEventData("OrderApproved", "ins-1", "biz-1", payload);
        var (eventName, instanceId, correlationId, deconstructedPayload) = data;

        Assert.Equal("OrderApproved", eventName);
        Assert.Equal("ins-1", instanceId);
        Assert.Equal("biz-1", correlationId);
        Assert.Same(payload, deconstructedPayload);
        Assert.Same(payload, data.Payload);
    }

    /// <summary>
    /// 自定义业务事件的相关性标识可为空（广播语义）
    /// </summary>
    [Fact]
    public void CustomEvent_AllowsNullCorrelationId()
    {
        var data = new WorkflowCustomEventData("OrderApproved", "ins-1", null, []);

        Assert.Null(data.CorrelationId);
        Assert.Empty(data.Payload);
    }

    /// <summary>
    /// 载荷内容相同但字典对象不同的自定义事件不相等
    /// </summary>
    [Fact]
    public void CustomEvent_Equality_UsesReferenceComparisonForPayload()
    {
        var first = new WorkflowCustomEventData("E", "ins-1", null, new Dictionary<string, object?> { ["a"] = 1 });
        var second = new WorkflowCustomEventData("E", "ins-1", null, new Dictionary<string, object?> { ["a"] = 1 });

        Assert.NotEqual(first, second);
    }
}
