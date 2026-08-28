// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.Abstractions.Tests;

/// <summary>
/// 事件类型与处理器工厂绑定关系测试
/// </summary>
/// <remarks>
/// 该类型是事件总线订阅表的行结构：事件总线在订阅、注销时会直接增删同一个工厂列表实例，
/// 因此构造时必须持有传入列表的引用而不是拷贝，否则后续注销将作用不到订阅表上。
/// </remarks>
public class EventTypeWithEventHandlerFactoriesTests
{
    /// <summary>
    /// 构造后事件类型原样暴露
    /// </summary>
    [Fact]
    public void Ctor_WithEventType_ExposesSameType()
    {
        var target = new EventTypeWithEventHandlerFactories(typeof(SampleEvent), []);

        Assert.Same(typeof(SampleEvent), target.EventType);
    }

    /// <summary>
    /// 构造后工厂列表以引用方式持有，不做防御性拷贝
    /// </summary>
    [Fact]
    public void Ctor_WithFactories_HoldsSameListReference()
    {
        var factories = new List<IEventHandlerFactory>
        {
            new SingleInstanceEventHandlerFactory(new RecordingLocalEventHandler())
        };

        var target = new EventTypeWithEventHandlerFactories(typeof(SampleEvent), factories);

        Assert.Same(factories, target.EventHandlerFactories);
    }

    /// <summary>
    /// 通过原始列表新增订阅可以从绑定关系中读到
    /// </summary>
    [Fact]
    public void EventHandlerFactories_WhenSourceListMutated_ReflectsChange()
    {
        var factories = new List<IEventHandlerFactory>();
        var target = new EventTypeWithEventHandlerFactories(typeof(SampleEvent), factories);
        var factory = new SingleInstanceEventHandlerFactory(new RecordingLocalEventHandler());

        factories.Add(factory);

        Assert.Single(target.EventHandlerFactories);
        Assert.Same(factory, target.EventHandlerFactories[0]);
    }

    /// <summary>
    /// 空工厂列表是合法状态，表示该事件类型暂无订阅
    /// </summary>
    [Fact]
    public void Ctor_WithEmptyFactories_IsAllowed()
    {
        var target = new EventTypeWithEventHandlerFactories(typeof(SampleEvent), []);

        Assert.Empty(target.EventHandlerFactories);
    }

    /// <summary>
    /// 工厂可借助绑定关系判断自身是否已在订阅表中
    /// </summary>
    [Fact]
    public void IsInFactories_WhenFactoryRegistered_ReturnsTrue()
    {
        var registered = new SingleInstanceEventHandlerFactory(new RecordingLocalEventHandler());
        var stranger = new SingleInstanceEventHandlerFactory(new RecordingLocalEventHandler());
        var target = new EventTypeWithEventHandlerFactories(
            typeof(SampleEvent),
            [registered]);

        Assert.True(registered.IsInFactories(target.EventHandlerFactories));
        Assert.False(stranger.IsInFactories(target.EventHandlerFactories));
    }
}
