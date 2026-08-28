// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.EventBus.Abstractions.Tests;

/// <summary>
/// 事件名称提供器契约测试
/// </summary>
/// <remarks>
/// 事件名是分布式事件的路由键，也是事件盒记录里唯一能跨进程识别事件类型的字段。
/// 抽象包只规定「由类型得名」这一条，具体命名策略由实现包决定；
/// 这里覆盖的是契约形状，以及一个按类型全名取名的参考实现必须满足的边界：
/// 得到的名称要能直接落进事件盒记录（不为空、不超长）。
/// </remarks>
public class EventNameProviderContractTests
{
    /// <summary>
    /// 契约只有一个由类型取名的方法，且是同步的
    /// </summary>
    [Fact]
    public void EventNameProvider_HasSingleSynchronousLookup()
    {
        var methods = typeof(IEventNameProvider).GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        var method = Assert.Single(methods);

        Assert.Equal(nameof(IEventNameProvider.GetName), method.Name);
        Assert.Equal(typeof(string), method.ReturnType);
        Assert.Equal(typeof(Type), Assert.Single(method.GetParameters()).ParameterType);
    }

    /// <summary>
    /// 按类型全名取名时，不同事件类型得到不同的名称
    /// </summary>
    [Fact]
    public void GetName_ForDistinctEventTypes_ReturnsDistinctNames()
    {
        IEventNameProvider provider = new FullNameEventNameProvider();

        var first = provider.GetName(typeof(SampleEvent));
        var second = provider.GetName(typeof(AnotherSampleEvent));

        Assert.NotEqual(first, second);
        Assert.Contains(nameof(SampleEvent), first);
        Assert.Contains(nameof(AnotherSampleEvent), second);
    }

    /// <summary>
    /// 同一事件类型多次取名结果稳定，路由键才不会漂移
    /// </summary>
    [Fact]
    public void GetName_ForSameEventType_IsStable()
    {
        IEventNameProvider provider = new FullNameEventNameProvider();

        Assert.Equal(provider.GetName(typeof(SampleEvent)), provider.GetName(typeof(SampleEvent)));
    }

    /// <summary>
    /// 取到的名称可以直接作为事件盒记录的事件名落库
    /// </summary>
    [Fact]
    public void GetName_Result_FitsEventBoxRecord()
    {
        IEventNameProvider provider = new FullNameEventNameProvider();
        var eventName = provider.GetName(typeof(SampleEvent));

        Assert.False(string.IsNullOrWhiteSpace(eventName));
        Assert.True(eventName.Length <= OutgoingEventInfo.MaxEventNameLength);

        var outgoing = EventInfoFactory.CreateOutgoing(eventName);

        Assert.Equal(eventName, outgoing.EventName);
    }
}

/// <summary>
/// 测试桩：按类型全名取事件名的参考实现
/// </summary>
public sealed class FullNameEventNameProvider : IEventNameProvider
{
    /// <summary>
    /// 获取事件名称
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <returns>事件名称</returns>
    public string GetName(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        return eventType.FullName ?? eventType.Name;
    }
}
