// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Tests.Fakes;

namespace XiHan.Framework.EventBus.Tests.Local;

/// <summary>
/// 本地事件总线自定义处理器工厂隔离测试
/// </summary>
/// <remarks>
/// 三个内置工厂都能在不实例化处理器的前提下给出类型，排序阶段不会碰 GetHandler；
/// 框架外自定义的工厂给不出类型，只能走 ResolveHandlerType 的回退分支实例化一次。
/// 这条回退路径此前既没有异常隔离——排序阶段位于 EventBusBase.TriggerHandlersAsync 逐处理器
/// try/catch 之外，自定义工厂在这里抛出会让同一事件的其余处理器一个都跑不成。
/// 本类锁住回退分支的两件事：取类型失败不连坐其他处理器，取到类型时包装器必须被归还。
/// </remarks>
public class LocalEventBusCustomFactoryIsolationTests
{
    /// <summary>
    /// 自定义工厂给不出处理器时查询工厂列表不抛异常
    /// </summary>
    /// <remarks>
    /// 与内置瞬时工厂「处理器构造不出来也能列出工厂」的既有契约对齐，查询是纯读操作，不该被解析失败带塌。
    /// </remarks>
    [Fact]
    public void GetEventHandlerFactories_WithThrowingCustomFactory_DoesNotThrow()
    {
        using var harness = LocalEventBusHarness.Create();
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), new ThrowingCustomEventHandlerFactory());

        Assert.Single(harness.Bus.GetEventHandlerFactories(typeof(PlainNoticeEvent)));
    }

    /// <summary>
    /// 自定义工厂给不出处理器时其余处理器照常被触发
    /// </summary>
    /// <remarks>
    /// 修复前排序阶段的异常直接穿出 PublishAsync，排在后面的处理器一个都轮不到。
    /// </remarks>
    [Fact]
    public async Task PublishAsync_WithThrowingCustomFactory_StillInvokesRemainingHandlers()
    {
        using var harness = LocalEventBusHarness.Create();
        var survivor = new RecordingLocalHandler<PlainNoticeEvent>();
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), new ThrowingCustomEventHandlerFactory());
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), survivor);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Bus.PublishAsync(new PlainNoticeEvent()));

        Assert.Equal("自定义工厂给不出处理器", exception.Message);
        Assert.Single(survivor.Received);
    }

    /// <summary>
    /// 一个失败的自定义工厂只贡献一条异常
    /// </summary>
    /// <remarks>
    /// 排序阶段吞掉的那次失败不能被重复计入，否则两个坏工厂会凑出四条内部异常。
    /// 异常也不能真的被吞掉：触发阶段还会解析一次，那次失败必须出现在聚合异常里。
    /// </remarks>
    [Fact]
    public async Task PublishAsync_WithTwoThrowingCustomFactories_ReportsOneExceptionEach()
    {
        using var harness = LocalEventBusHarness.Create();
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), new ThrowingCustomEventHandlerFactory());
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), new ThrowingCustomEventHandlerFactory());

        var exception = await Assert.ThrowsAsync<AggregateException>(
            () => harness.Bus.PublishAsync(new PlainNoticeEvent()));

        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.All(exception.InnerExceptions, item => Assert.Equal("自定义工厂给不出处理器", item.Message));
    }

    /// <summary>
    /// 回退分支实例化出来的包装器全部被归还
    /// </summary>
    /// <remarks>
    /// 回退分支必须用 using 包住 GetHandler，否则自定义工厂的归还动作永远不会执行，
    /// 与 IoC 工厂当初泄漏 DI 作用域是同一个毛病。
    /// </remarks>
    [Fact]
    public async Task PublishAsync_WithCustomFactory_DisposesEveryHandlerWrapper()
    {
        using var harness = LocalEventBusHarness.Create();
        var factory = new CountingCustomEventHandlerFactory(new RecordingLocalHandler<PlainNoticeEvent>());
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), factory);

        await harness.Bus.PublishAsync(new PlainNoticeEvent());

        Assert.True(factory.Created > 0);
        Assert.Equal(factory.Created, factory.Disposed);
    }

    /// <summary>
    /// 回退分支仍能读到处理器声明的顺序特性
    /// </summary>
    /// <remarks>
    /// 回退分支是唯一还需要实例化才能取类型的路径，加上 try/catch 之后不能把排序能力一起弄丢。
    /// </remarks>
    [Fact]
    public async Task PublishAsync_WithCustomFactories_StillOrdersByOrderAttribute()
    {
        using var harness = LocalEventBusHarness.Create();
        var trace = new List<string>();

        // 故意按与期望相反的次序订阅，确保断言检验的是排序而不是订阅顺序
        harness.Bus.Subscribe(
            typeof(PlainNoticeEvent),
            new CountingCustomEventHandlerFactory(new LateOrderedHandler(trace)));
        harness.Bus.Subscribe(
            typeof(PlainNoticeEvent),
            new CountingCustomEventHandlerFactory(new EarlyOrderedHandler(trace)));

        await harness.Bus.PublishAsync(new PlainNoticeEvent());

        Assert.Equal(new[] { nameof(EarlyOrderedHandler), nameof(LateOrderedHandler) }, trace);
    }

    /// <summary>
    /// 取不到类型的工厂按顺序 0 处理，不打乱其他处理器的次序
    /// </summary>
    /// <remarks>
    /// 反例：坏工厂拿不到类型，只能退回默认顺序，但它既不能把自己插到声明了顺序的处理器之间，
    /// 也不能影响其余处理器彼此的先后。
    /// </remarks>
    [Fact]
    public async Task PublishAsync_WithThrowingCustomFactory_KeepsRemainingHandlersOrdered()
    {
        using var harness = LocalEventBusHarness.Create();
        var trace = new List<string>();
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), new ThrowingCustomEventHandlerFactory());
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), new LateOrderedHandler(trace));
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), new EarlyOrderedHandler(trace));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Bus.PublishAsync(new PlainNoticeEvent()));

        Assert.Equal(new[] { nameof(EarlyOrderedHandler), nameof(LateOrderedHandler) }, trace);
    }
}

/// <summary>
/// 测试替身：框架外自定义的、永远给不出处理器的事件处理器工厂
/// </summary>
/// <remarks>
/// 不是三个内置工厂中的任何一个，因此排序阶段只能走 ResolveHandlerType 的回退分支。
/// </remarks>
public sealed class ThrowingCustomEventHandlerFactory : IEventHandlerFactory
{
    /// <summary>
    /// 获取事件处理器
    /// </summary>
    /// <returns>不会返回，总是抛出异常</returns>
    public IEventHandlerDisposeWrapper GetHandler()
    {
        throw new InvalidOperationException("自定义工厂给不出处理器");
    }

    /// <summary>
    /// 判断当前工厂是否已存在于工厂列表中
    /// </summary>
    /// <param name="handlerFactories">事件处理器工厂列表</param>
    /// <returns>恒为 false，便于同时订阅多个实例</returns>
    public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
    {
        return false;
    }
}

/// <summary>
/// 测试替身：框架外自定义的、统计取用与归还次数的事件处理器工厂
/// </summary>
/// <remarks>
/// 始终返回同一个处理器实例，只统计包装器被取用与被释放的次数，用于验证回退分支有没有归还包装器。
/// </remarks>
public sealed class CountingCustomEventHandlerFactory : IEventHandlerFactory
{
    private readonly IEventHandler _handler;

    private int _created;
    private int _disposed;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="handler">被包装的事件处理器</param>
    public CountingCustomEventHandlerFactory(IEventHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// 包装器被取用的次数
    /// </summary>
    public int Created => Volatile.Read(ref _created);

    /// <summary>
    /// 包装器被释放的次数
    /// </summary>
    public int Disposed => Volatile.Read(ref _disposed);

    /// <summary>
    /// 获取事件处理器
    /// </summary>
    /// <returns>包装了事件处理器的释放包装器</returns>
    public IEventHandlerDisposeWrapper GetHandler()
    {
        Interlocked.Increment(ref _created);
        return new EventHandlerDisposeWrapper(_handler, () => Interlocked.Increment(ref _disposed));
    }

    /// <summary>
    /// 判断当前工厂是否已存在于工厂列表中
    /// </summary>
    /// <param name="handlerFactories">事件处理器工厂列表</param>
    /// <returns>恒为 false，便于同时订阅多个实例</returns>
    public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
    {
        return false;
    }
}
