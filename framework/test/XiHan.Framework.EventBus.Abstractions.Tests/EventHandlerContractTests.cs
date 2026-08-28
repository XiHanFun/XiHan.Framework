// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;

namespace XiHan.Framework.EventBus.Abstractions.Tests;

/// <summary>
/// 事件处理器接口契约测试
/// </summary>
/// <remarks>
/// 事件总线在运行期靠「扫描处理器类型实现了哪些 <c>ILocalEventHandler&lt;&gt;</c> / <c>IDistributedEventHandler&lt;&gt;</c> 闭合接口」
/// 来反推它能处理的事件类型，这里覆盖的就是这条推断链所依赖的形状约定：
/// 标记接口无成员、泛型参数逆变、闭合接口可用运行时事件类型构造。
/// </remarks>
public class EventHandlerContractTests
{
    /// <summary>
    /// 处理器根接口是纯标记接口，不携带任何成员
    /// </summary>
    [Fact]
    public void EventHandler_IsMarkerInterfaceWithoutMembers()
    {
        var members = typeof(IEventHandler).GetMembers(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.True(typeof(IEventHandler).IsInterface);
        Assert.Empty(members);
    }

    /// <summary>
    /// 本地与分布式处理器都归拢到同一标记接口下
    /// </summary>
    [Theory]
    [InlineData(typeof(ILocalEventHandler<>))]
    [InlineData(typeof(IDistributedEventHandler<>))]
    public void EventHandlerInterfaces_DeriveFromMarkerInterface(Type handlerInterface)
    {
        Assert.Contains(typeof(IEventHandler), handlerInterface.GetInterfaces());
    }

    /// <summary>
    /// 处理器泛型参数是逆变的，父类处理器可当作子类处理器使用
    /// </summary>
    [Theory]
    [InlineData(typeof(ILocalEventHandler<>))]
    [InlineData(typeof(IDistributedEventHandler<>))]
    public void EventHandlerInterfaces_GenericArgumentIsContravariant(Type handlerInterface)
    {
        var genericArgument = handlerInterface.GetGenericArguments()[0];

        Assert.True(genericArgument.GenericParameterAttributes
            .HasFlag(GenericParameterAttributes.Contravariant));
    }

    /// <summary>
    /// 逆变在编译期与运行期都成立
    /// </summary>
    [Fact]
    public void LocalEventHandler_ObjectHandler_IsUsableAsConcreteHandler()
    {
        ILocalEventHandler<object> objectHandler = new ObjectLocalEventHandler();

        // 逆变让「能处理 object 的处理器」自动满足「能处理 SampleEvent」的位置
        ILocalEventHandler<SampleEvent> narrowedHandler = objectHandler;

        Assert.Same(objectHandler, narrowedHandler);
        Assert.True(typeof(ILocalEventHandler<object>).IsAssignableTo(typeof(ILocalEventHandler<SampleEvent>)));
    }

    /// <summary>
    /// 从处理器类型可反推出它声明处理的全部本地事件类型
    /// </summary>
    [Fact]
    public void HandlerType_LocalEventTypes_AreDiscoverableFromInterfaces()
    {
        var eventTypes = typeof(MultiEventHandler)
            .GetInterfaces()
            .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ILocalEventHandler<>))
            .Select(x => x.GenericTypeArguments[0])
            .ToList();

        Assert.Equal(2, eventTypes.Count);
        Assert.Contains(typeof(SampleEvent), eventTypes);
        Assert.Contains(typeof(AnotherSampleEvent), eventTypes);
    }

    /// <summary>
    /// 本地与分布式两套订阅互不串台
    /// </summary>
    [Fact]
    public void HandlerType_DistributedEventTypes_AreDiscoveredSeparately()
    {
        var eventTypes = typeof(MultiEventHandler)
            .GetInterfaces()
            .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IDistributedEventHandler<>))
            .Select(x => x.GenericTypeArguments[0])
            .ToList();

        Assert.Equal(typeof(SampleEvent), Assert.Single(eventTypes));
    }

    /// <summary>
    /// 处理方法固定返回 Task，事件总线可以直接 await
    /// </summary>
    [Theory]
    [InlineData(typeof(ILocalEventHandler<>))]
    [InlineData(typeof(IDistributedEventHandler<>))]
    public void HandleEventAsync_ReturnsTask(Type handlerInterface)
    {
        var method = handlerInterface.GetMethod("HandleEventAsync");

        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method.ReturnType);
        Assert.Single(method.GetParameters());
    }

    /// <summary>
    /// 用运行时事件类型构造闭合处理器接口后可直接反射派发
    /// </summary>
    [Fact]
    public async Task Invoker_WithRuntimeEventType_DispatchesToMatchingHandler()
    {
        var handler = new MultiEventHandler();
        var invoker = new ReflectionEventHandlerInvoker();
        var eventData = new SampleEvent { Payload = "dispatched" };

        await invoker.InvokeAsync(handler, eventData, typeof(SampleEvent));

        Assert.Equal(1, invoker.InvokedCount);
        Assert.Same(eventData, Assert.Single(handler.Handled));
    }

    /// <summary>
    /// 事件类型与处理器声明不匹配时不派发
    /// </summary>
    [Fact]
    public async Task Invoker_WithUnhandledEventType_DoesNotDispatch()
    {
        var handler = new RecordingLocalEventHandler();
        var invoker = new ReflectionEventHandlerInvoker();

        await invoker.InvokeAsync(handler, new AnotherSampleEvent(), typeof(AnotherSampleEvent));

        Assert.Equal(0, invoker.InvokedCount);
        Assert.Empty(handler.Handled);
    }

    /// <summary>
    /// 释放包装持有原处理器，且释放动作可被观察
    /// </summary>
    [Fact]
    public void DisposeWrapper_AfterDispose_MarksDisposedAndKeepsHandler()
    {
        var handler = new RecordingLocalEventHandler();
        var wrapper = new FakeEventHandlerDisposeWrapper(handler);

        Assert.Same(handler, wrapper.EventHandler);
        Assert.False(wrapper.IsDisposed);

        wrapper.Dispose();

        Assert.True(wrapper.IsDisposed);
    }

    /// <summary>
    /// 释放包装本身是 IDisposable，才能被 using 语句接管
    /// </summary>
    [Fact]
    public void DisposeWrapper_IsDisposable()
    {
        Assert.Contains(typeof(IDisposable), typeof(IEventHandlerDisposeWrapper).GetInterfaces());
    }

    /// <summary>
    /// 工厂每次发放新的释放包装，但复用同一处理器实例
    /// </summary>
    [Fact]
    public void HandlerFactory_GetHandler_IssuesNewWrapperPerCall()
    {
        var handler = new RecordingLocalEventHandler();
        var factory = new SingleInstanceEventHandlerFactory(handler);

        var first = factory.GetHandler();
        var second = factory.GetHandler();

        Assert.Equal(2, factory.GetHandlerCount);
        Assert.NotSame(first, second);
        Assert.Same(handler, first.EventHandler);
        Assert.Same(handler, second.EventHandler);
    }
}
