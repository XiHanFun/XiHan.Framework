// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.EventBus.Tests.Fakes;

namespace XiHan.Framework.EventBus.Tests.Local;

/// <summary>
/// 本地事件总线处理器生命周期测试
/// </summary>
/// <remarks>
/// 锁住两条修复：
/// 一是排序阶段（GetHandlerFactories）不再为了读一个顺序特性而实例化处理器——原实现调用
/// factory.GetHandler() 后从不释放包装器，IoC 工厂因此每发布一次事件就泄漏一个 DI 作用域；
/// 二是处理器「建不出来」与「执行失败」一样被收进异常列表，不再让整条触发链一个处理器都跑不成。
/// 计数全部走注册进容器的探针，不用静态字段，避免用例之间互相污染。
/// </remarks>
public class LocalEventBusHandlerLifetimeTests
{
    /// <summary>
    /// 发布一次事件只解析一次 IoC 处理器
    /// </summary>
    /// <remarks>
    /// 修复前排序阶段与触发阶段各解析一次，共开两个作用域，其中排序阶段那个永不释放。
    /// </remarks>
    [Fact]
    public async Task PublishAsync_WithIocFactory_ResolvesHandlerOncePerPublish()
    {
        var counter = new LifetimeProbeCounter();
        using var harness = CreateHarness(counter);
        harness.Bus.Subscribe(
            typeof(PlainNoticeEvent),
            new IocEventHandlerFactory(harness.ServiceScopeFactory, typeof(LifetimeCountingHandler)));

        await harness.Bus.PublishAsync(new PlainNoticeEvent());

        Assert.Equal(1, counter.Created);
    }

    /// <summary>
    /// 发布过程中开出来的作用域全部被释放
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithIocFactory_DisposesEveryResolvedScope()
    {
        var counter = new LifetimeProbeCounter();
        using var harness = CreateHarness(counter);
        harness.Bus.Subscribe(
            typeof(PlainNoticeEvent),
            new IocEventHandlerFactory(harness.ServiceScopeFactory, typeof(LifetimeCountingHandler)));

        await harness.Bus.PublishAsync(new PlainNoticeEvent());

        Assert.Equal(counter.Created, counter.Disposed);
    }

    /// <summary>
    /// 反复发布不会累积未释放的作用域
    /// </summary>
    /// <remarks>
    /// 泄漏是随发布量线性增长的，只发一次看不出斜率，这里连发三次把增量固定住。
    /// </remarks>
    [Fact]
    public async Task PublishAsync_RepeatedlyWithIocFactory_DoesNotAccumulateScopes()
    {
        var counter = new LifetimeProbeCounter();
        using var harness = CreateHarness(counter);
        harness.Bus.Subscribe(
            typeof(PlainNoticeEvent),
            new IocEventHandlerFactory(harness.ServiceScopeFactory, typeof(LifetimeCountingHandler)));

        for (var index = 0; index < 3; index++)
        {
            await harness.Bus.PublishAsync(new PlainNoticeEvent());
        }

        Assert.Equal(3, counter.Created);
        Assert.Equal(3, counter.Disposed);
    }

    /// <summary>
    /// 只查询处理器工厂列表不会实例化任何处理器
    /// </summary>
    [Fact]
    public void GetEventHandlerFactories_WithIocFactory_DoesNotResolveHandler()
    {
        var counter = new LifetimeProbeCounter();
        using var harness = CreateHarness(counter);
        harness.Bus.Subscribe(
            typeof(PlainNoticeEvent),
            new IocEventHandlerFactory(harness.ServiceScopeFactory, typeof(LifetimeCountingHandler)));

        Assert.Single(harness.Bus.GetEventHandlerFactories(typeof(PlainNoticeEvent)));
        Assert.Equal(0, counter.Created);
    }

    /// <summary>
    /// 处理器根本构造不出来时也能列出工厂
    /// </summary>
    /// <remarks>
    /// ProbeAwareLocalHandler 没有无参构造函数，瞬时工厂一旦实例化必抛。
    /// 这条不用计数器，直接以「不抛异常」反证排序阶段确实没有实例化处理器。
    /// </remarks>
    [Fact]
    public void GetEventHandlerFactories_WithUnconstructibleTransientHandler_DoesNotThrow()
    {
        using var harness = LocalEventBusHarness.Create();
        harness.Bus.Subscribe(
            typeof(PlainNoticeEvent),
            new TransientEventHandlerFactory(typeof(ProbeAwareLocalHandler)));

        Assert.Single(harness.Bus.GetEventHandlerFactories(typeof(PlainNoticeEvent)));
    }

    /// <summary>
    /// 不实例化也要能读到 IoC 处理器声明的顺序特性
    /// </summary>
    /// <remarks>
    /// 顺序值改为从工厂已知的处理器类型上读取，这条确保改法没有把排序能力一起弄丢。
    /// </remarks>
    [Fact]
    public async Task PublishAsync_WithIocFactories_StillOrdersByOrderAttribute()
    {
        var trace = new List<string>();
        using var harness = LocalEventBusHarness.Create(services =>
        {
            services.AddSingleton(trace);
            services.AddTransient<EarlyOrderedHandler>();
            services.AddTransient<LateOrderedHandler>();
        });

        // 故意按与期望相反的次序订阅，确保断言检验的是排序而不是订阅顺序
        harness.Bus.Subscribe(
            typeof(PlainNoticeEvent),
            new IocEventHandlerFactory(harness.ServiceScopeFactory, typeof(LateOrderedHandler)));
        harness.Bus.Subscribe(
            typeof(PlainNoticeEvent),
            new IocEventHandlerFactory(harness.ServiceScopeFactory, typeof(EarlyOrderedHandler)));

        await harness.Bus.PublishAsync(new PlainNoticeEvent());

        Assert.Equal(new[] { nameof(EarlyOrderedHandler), nameof(LateOrderedHandler) }, trace);
    }

    /// <summary>
    /// 某个处理器解析不出来时其余处理器照常被触发
    /// </summary>
    /// <remarks>
    /// 修复前解析失败发生在异常隔离范围之外，整个发布直接抛出，后面的处理器一个都轮不到。
    /// </remarks>
    [Fact]
    public async Task PublishAsync_WhenHandlerCannotBeResolved_StillInvokesRemainingHandlers()
    {
        using var harness = LocalEventBusHarness.Create();
        var survivor = new RecordingLocalHandler<PlainNoticeEvent>();
        // 容器里没有登记 ParameterlessLocalHandler，IoC 工厂解析时必抛
        harness.Bus.Subscribe(
            typeof(PlainNoticeEvent),
            new IocEventHandlerFactory(harness.ServiceScopeFactory, typeof(ParameterlessLocalHandler)));
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), survivor);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Bus.PublishAsync(new PlainNoticeEvent()));

        Assert.Contains("无法从 IoC 容器解析事件处理器", exception.Message, StringComparison.Ordinal);
        Assert.Single(survivor.Received);
    }

    /// <summary>
    /// 某个处理器构造不出来时其余处理器照常被触发
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenHandlerCannotBeConstructed_StillInvokesRemainingHandlers()
    {
        using var harness = LocalEventBusHarness.Create();
        var survivor = new RecordingLocalHandler<PlainNoticeEvent>();
        harness.Bus.Subscribe(
            typeof(PlainNoticeEvent),
            new TransientEventHandlerFactory(typeof(ProbeAwareLocalHandler)));
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), survivor);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Bus.PublishAsync(new PlainNoticeEvent()));

        Assert.Contains("无参构造函数", exception.Message, StringComparison.Ordinal);
        Assert.Single(survivor.Received);
    }

    /// <summary>
    /// 创建失败与执行失败混在一起时按聚合异常一次性抛出
    /// </summary>
    /// <remarks>
    /// 「建不出来」应当和「执行抛异常」被同等对待，都进 exceptions 列表，
    /// 而不是一个提前中断触发链、一个走收集路径。
    /// </remarks>
    [Fact]
    public async Task PublishAsync_WhenCreationAndExecutionBothFail_CollectsBothIntoAggregate()
    {
        using var harness = LocalEventBusHarness.Create();
        var failing = new ThrowingLocalHandler<PlainNoticeEvent> { FailureMessage = "执行失败" };
        var survivor = new RecordingLocalHandler<PlainNoticeEvent>();
        harness.Bus.Subscribe(
            typeof(PlainNoticeEvent),
            new IocEventHandlerFactory(harness.ServiceScopeFactory, typeof(ParameterlessLocalHandler)));
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), failing);
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), survivor);

        var exception = await Assert.ThrowsAsync<AggregateException>(
            () => harness.Bus.PublishAsync(new PlainNoticeEvent()));

        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.Contains(exception.InnerExceptions, item => item.Message == "执行失败");
        Assert.Contains(
            exception.InnerExceptions,
            item => item.Message.Contains("无法从 IoC 容器解析事件处理器", StringComparison.Ordinal));
        Assert.Single(survivor.Received);
    }

    /// <summary>
    /// 单实例订阅的处理器仍按实例的运行时类型读取顺序特性
    /// </summary>
    /// <remarks>
    /// 单实例工厂本来就持有实例，改法对它不引入任何行为差异，这里作为反例守住。
    /// </remarks>
    [Fact]
    public async Task PublishAsync_WithSingleInstanceHandlers_StillOrdersByOrderAttribute()
    {
        using var harness = LocalEventBusHarness.Create();
        var trace = new List<string>();
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), new LateOrderedHandler(trace));
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), new EarlyOrderedHandler(trace));

        await harness.Bus.PublishAsync(new PlainNoticeEvent());

        Assert.Equal(new[] { nameof(EarlyOrderedHandler), nameof(LateOrderedHandler) }, trace);
    }

    private static LocalEventBusHarness CreateHarness(LifetimeProbeCounter counter)
    {
        return LocalEventBusHarness.Create(services =>
        {
            services.AddSingleton(counter);
            services.AddScoped<LifetimeScopedProbe>();
            services.AddTransient<LifetimeCountingHandler>();
        });
    }
}

/// <summary>
/// 测试替身：统计作用域探针的创建与释放次数
/// </summary>
/// <remarks>
/// 以容器单例的形式注入，避免静态字段在并行用例之间串味。
/// </remarks>
public sealed class LifetimeProbeCounter
{
    private int _created;
    private int _disposed;

    /// <summary>
    /// 创建次数
    /// </summary>
    public int Created => Volatile.Read(ref _created);

    /// <summary>
    /// 释放次数
    /// </summary>
    public int Disposed => Volatile.Read(ref _disposed);

    /// <summary>
    /// 记录一次创建
    /// </summary>
    public void MarkCreated()
    {
        Interlocked.Increment(ref _created);
    }

    /// <summary>
    /// 记录一次释放
    /// </summary>
    public void MarkDisposed()
    {
        Interlocked.Increment(ref _disposed);
    }
}

/// <summary>
/// 测试替身：作用域内的计数探针
/// </summary>
/// <remarks>
/// 登记为 Scoped，因此它的创建次数等于 IoC 工厂开出的作用域数量，释放次数等于被释放的作用域数量。
/// </remarks>
public sealed class LifetimeScopedProbe : IDisposable
{
    private readonly LifetimeProbeCounter _counter;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="counter">计数器</param>
    public LifetimeScopedProbe(LifetimeProbeCounter counter)
    {
        _counter = counter;
        _counter.MarkCreated();
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _counter.MarkDisposed();
    }
}

/// <summary>
/// 测试替身：依赖作用域计数探针的本地事件处理器
/// </summary>
public sealed class LifetimeCountingHandler : ILocalEventHandler<PlainNoticeEvent>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="probe">作用域计数探针</param>
    public LifetimeCountingHandler(LifetimeScopedProbe probe)
    {
        Probe = probe;
    }

    /// <summary>
    /// 作用域计数探针
    /// </summary>
    public LifetimeScopedProbe Probe { get; }

    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    public Task HandleEventAsync(PlainNoticeEvent eventData) => Task.CompletedTask;
}
