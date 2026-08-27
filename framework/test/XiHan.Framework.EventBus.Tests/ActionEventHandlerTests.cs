// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.EventBus.Tests.Fakes;

namespace XiHan.Framework.EventBus.Tests;

/// <summary>
/// 委托事件处理器测试
/// </summary>
/// <remarks>
/// 委托处理器是「按委托订阅 / 按委托退订」的载体，退订依赖 <c>Action</c> 属性与原委托引用相等，
/// 因此这里连同该属性一并锁定。
/// </remarks>
public class ActionEventHandlerTests
{
    /// <summary>
    /// 委托为空时构造失败
    /// </summary>
    [Fact]
    public void Ctor_WhenHandlerNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new ActionEventHandler<PlainNoticeEvent>(null!);
        });
    }

    /// <summary>
    /// 委托属性与构造时传入的引用相同
    /// </summary>
    /// <remarks>
    /// 本地事件总线按 <c>actionHandler.Action == action</c> 退订，引用不一致会导致退订静默失效。
    /// </remarks>
    [Fact]
    public void Action_IsSameReferenceAsCtorArgument()
    {
        Func<PlainNoticeEvent, Task> action = _ => Task.CompletedTask;

        var handler = new ActionEventHandler<PlainNoticeEvent>(action);

        Assert.Same(action, handler.Action);
    }

    /// <summary>
    /// 处理事件时把事件数据原样交给委托
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_PassesEventDataToAction()
    {
        PlainNoticeEvent? captured = null;
        var handler = new ActionEventHandler<PlainNoticeEvent>(eventData =>
        {
            captured = eventData;
            return Task.CompletedTask;
        });
        var payload = new PlainNoticeEvent { Message = "委托处理" };

        await handler.HandleEventAsync(payload);

        Assert.Same(payload, captured);
    }

    /// <summary>
    /// 委托抛出的异常原样冒泡
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_WhenActionFails_PropagatesException()
    {
        var handler = new ActionEventHandler<PlainNoticeEvent>(
            _ => Task.FromException(new InvalidOperationException("委托失败")));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleEventAsync(new PlainNoticeEvent()));

        Assert.Equal("委托失败", exception.Message);
    }

    /// <summary>
    /// 事件数据为空时拒绝处理
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_WhenEventDataNull_Throws()
    {
        var invoked = false;
        var handler = new ActionEventHandler<PlainNoticeEvent>(_ =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.HandleEventAsync(null!));

        Assert.False(invoked);
    }

    /// <summary>
    /// 委托处理器同时满足本地处理器契约与瞬时生命周期约定
    /// </summary>
    [Fact]
    public void Type_SatisfiesLocalHandlerAndTransientContracts()
    {
        var handler = new ActionEventHandler<PlainNoticeEvent>(_ => Task.CompletedTask);

        Assert.IsAssignableFrom<ILocalEventHandler<PlainNoticeEvent>>(handler);
        Assert.IsAssignableFrom<ITransientDependency>(handler);
    }
}
