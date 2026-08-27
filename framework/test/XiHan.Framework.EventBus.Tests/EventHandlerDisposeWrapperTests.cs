// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Tests.Fakes;

namespace XiHan.Framework.EventBus.Tests;

/// <summary>
/// 事件处理器释放包装器测试
/// </summary>
/// <remarks>
/// 包装器是「处理器实例 + 归还动作」的载体，各工厂靠它把作用域/瞬时实例的清理挂到调用方的 using 上。
/// </remarks>
public class EventHandlerDisposeWrapperTests
{
    /// <summary>
    /// 包装器原样暴露构造时传入的处理器
    /// </summary>
    [Fact]
    public void EventHandler_ReturnsProvidedHandler()
    {
        var handler = new ParameterlessLocalHandler();

        var wrapper = new EventHandlerDisposeWrapper(handler);

        Assert.Same(handler, wrapper.EventHandler);
        Assert.IsAssignableFrom<IEventHandlerDisposeWrapper>(wrapper);
    }

    /// <summary>
    /// 构造包装器本身不会触发归还动作
    /// </summary>
    [Fact]
    public void Ctor_DoesNotInvokeDisposeAction()
    {
        var invoked = 0;

        _ = new EventHandlerDisposeWrapper(new ParameterlessLocalHandler(), () => invoked++);

        Assert.Equal(0, invoked);
    }

    /// <summary>
    /// 释放包装器会执行归还动作
    /// </summary>
    [Fact]
    public void Dispose_WhenActionProvided_InvokesAction()
    {
        var invoked = 0;
        var wrapper = new EventHandlerDisposeWrapper(new ParameterlessLocalHandler(), () => invoked++);

        wrapper.Dispose();

        Assert.Equal(1, invoked);
    }

    /// <summary>
    /// 未提供归还动作时释放不抛异常
    /// </summary>
    [Fact]
    public void Dispose_WithoutAction_DoesNotThrow()
    {
        var wrapper = new EventHandlerDisposeWrapper(new ParameterlessLocalHandler());

        wrapper.Dispose();
    }
}
