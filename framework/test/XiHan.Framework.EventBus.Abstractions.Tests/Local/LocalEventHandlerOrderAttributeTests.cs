// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.EventBus.Abstractions.Local;

namespace XiHan.Framework.EventBus.Abstractions.Tests.Local;

/// <summary>
/// 本地事件处理器顺序特性测试
/// </summary>
/// <remarks>
/// 本地事件的多个处理器按该特性排序执行，未标注时事件总线按默认顺序处理。
/// 特性的 <c>Inherited = true</c> 让派生处理器自动沿用基类顺序，这是排序稳定性的前提，需锁死。
/// </remarks>
public class LocalEventHandlerOrderAttributeTests
{
    /// <summary>
    /// 构造函数写入的顺序值原样暴露
    /// </summary>
    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(int.MaxValue)]
    public void Ctor_WithOrder_ExposesSameValue(int order)
    {
        var attribute = new LocalEventHandlerOrderAttribute(order);

        Assert.Equal(order, attribute.Order);
    }

    /// <summary>
    /// 顺序值可在构造后改写，允许运行期重排
    /// </summary>
    [Fact]
    public void Order_IsMutableAfterConstruction()
    {
        var attribute = new LocalEventHandlerOrderAttribute(1)
        {
            Order = 99
        };

        Assert.Equal(99, attribute.Order);
    }

    /// <summary>
    /// 标注在处理器类上后可被反射读出
    /// </summary>
    [Fact]
    public void Attribute_OnHandlerType_IsDiscoverable()
    {
        var attribute = typeof(OrderedLocalEventHandler)
            .GetCustomAttribute<LocalEventHandlerOrderAttribute>(false);

        Assert.NotNull(attribute);
        Assert.Equal(10, attribute.Order);
    }

    /// <summary>
    /// 派生处理器未标注时继承基类顺序
    /// </summary>
    [Fact]
    public void Attribute_OnDerivedHandlerType_IsInherited()
    {
        var attribute = typeof(DerivedOrderedLocalEventHandler)
            .GetCustomAttribute<LocalEventHandlerOrderAttribute>(true);

        Assert.NotNull(attribute);
        Assert.Equal(10, attribute.Order);
    }

    /// <summary>
    /// 派生处理器可覆盖基类顺序
    /// </summary>
    [Fact]
    public void Attribute_OnOverridingHandlerType_UsesOwnValue()
    {
        var attribute = typeof(ReorderedLocalEventHandler)
            .GetCustomAttribute<LocalEventHandlerOrderAttribute>(false);

        Assert.NotNull(attribute);
        Assert.Equal(-5, attribute.Order);
    }

    /// <summary>
    /// 未标注的处理器读不到特性，事件总线据此走默认顺序
    /// </summary>
    [Fact]
    public void Attribute_OnUnannotatedHandlerType_IsAbsent()
    {
        var attribute = typeof(RecordingLocalEventHandler)
            .GetCustomAttribute<LocalEventHandlerOrderAttribute>(true);

        Assert.Null(attribute);
    }

    /// <summary>
    /// 特性用法：只能标注在类上、不可重复、可被继承
    /// </summary>
    [Fact]
    public void AttributeUsage_IsPinned()
    {
        var usage = typeof(LocalEventHandlerOrderAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>(false);

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.True(usage.Inherited);
    }
}

/// <summary>
/// 测试桩：标注了执行顺序的本地事件处理器
/// </summary>
[LocalEventHandlerOrder(10)]
public class OrderedLocalEventHandler : ILocalEventHandler<SampleEvent>
{
    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    public virtual Task HandleEventAsync(SampleEvent eventData)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// 测试桩：未重新标注顺序的派生处理器
/// </summary>
public sealed class DerivedOrderedLocalEventHandler : OrderedLocalEventHandler
{
}

/// <summary>
/// 测试桩：重新标注顺序的派生处理器
/// </summary>
[LocalEventHandlerOrder(-5)]
public sealed class ReorderedLocalEventHandler : OrderedLocalEventHandler
{
}
