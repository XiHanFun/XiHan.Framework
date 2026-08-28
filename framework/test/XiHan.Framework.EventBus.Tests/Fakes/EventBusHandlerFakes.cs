// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.EventBus.Attributes;
using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.EventBus.Local;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.EventBus.Tests.Fakes;

#region 事件契约

/// <summary>
/// 测试事件：未声明事件名特性，事件名应回落到类型全名
/// </summary>
public class PlainNoticeEvent
{
    /// <summary>
    /// 载荷
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 测试事件：<see cref="PlainNoticeEvent"/> 的派生事件，用于验证按继承关系触发处理器
/// </summary>
public class DerivedNoticeEvent : PlainNoticeEvent
{
}

/// <summary>
/// 测试事件：显式声明事件名，事件名不随类型重命名漂移
/// </summary>
[EventName("xihan.tests.named-notice")]
public class NamedNoticeEvent
{
    /// <summary>
    /// 约定的事件名，与类型上的特性保持一致
    /// </summary>
    public const string DeclaredEventName = "xihan.tests.named-notice";

    /// <summary>
    /// 载荷
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 测试事件：泛型事件，事件名由泛型参数的事件名加前后缀拼装
/// </summary>
/// <typeparam name="TPayload">载荷类型</typeparam>
[GenericEventName(Prefix = "xihan.", Postfix = ".created")]
public class GenericNoticeEvent<TPayload>
{
    /// <summary>
    /// 载荷
    /// </summary>
    public TPayload? Payload { get; set; }
}

/// <summary>
/// 测试事件：泛型事件，未配置前后缀，事件名应与泛型参数事件名一致
/// </summary>
/// <typeparam name="TPayload">载荷类型</typeparam>
[GenericEventName]
public class BareGenericNoticeEvent<TPayload>
{
    /// <summary>
    /// 载荷
    /// </summary>
    public TPayload? Payload { get; set; }
}

/// <summary>
/// 测试事件：泛型参数可沿继承链向上级联的事件
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <remarks>
/// 触发 <c>InheritableNoticeEvent&lt;Derived&gt;</c> 时，框架应额外触发 <c>InheritableNoticeEvent&lt;Base&gt;</c>。
/// </remarks>
public class InheritableNoticeEvent<TEntity> : IEventDataWithInheritableGenericArgument
    where TEntity : class
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="entity">实体</param>
    public InheritableNoticeEvent(TEntity entity)
    {
        Entity = entity;
    }

    /// <summary>
    /// 实体
    /// </summary>
    public TEntity Entity { get; }

    /// <summary>
    /// 获取用于重建基类版本事件的构造参数
    /// </summary>
    /// <returns>构造参数</returns>
    public object[] GetConstructorArgs() => [Entity];
}

/// <summary>
/// 测试事件：未实现多租户接口，但带有普通的 TenantId 属性
/// </summary>
public class PlainTenantEvent
{
    /// <summary>
    /// 租户唯一标识
    /// </summary>
    public long? TenantId { get; set; }
}

/// <summary>
/// 测试事件：实现 <see cref="IMultiTenant"/> 的多租户事件
/// </summary>
public class MultiTenantNoticeEvent : IMultiTenant
{
    /// <summary>
    /// 租户唯一标识
    /// </summary>
    public long? TenantId { get; set; }
}

/// <summary>
/// 测试事件：实现 <see cref="IEventDataMayHaveTenantId"/> 的可选多租户事件
/// </summary>
public class MaybeTenantNoticeEvent : IEventDataMayHaveTenantId
{
    /// <summary>
    /// 是否声明为多租户事件
    /// </summary>
    public bool HasTenant { get; set; }

    /// <summary>
    /// 声明的租户唯一标识
    /// </summary>
    public long? DeclaredTenantId { get; set; }

    /// <summary>
    /// 判断是否为多租户事件
    /// </summary>
    /// <param name="tenantId">租户唯一标识</param>
    /// <returns>是否为多租户事件</returns>
    public bool IsMultiTenant(out long? tenantId)
    {
        tenantId = DeclaredTenantId;
        return HasTenant;
    }
}

#endregion 事件契约

#region 事件处理器

/// <summary>
/// 测试替身：记录收到的本地事件
/// </summary>
/// <typeparam name="TEvent">事件类型</typeparam>
public class RecordingLocalHandler<TEvent> : ILocalEventHandler<TEvent>
    where TEvent : class
{
    /// <summary>
    /// 按到达顺序记录的事件
    /// </summary>
    public List<TEvent> Received { get; } = [];

    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    public Task HandleEventAsync(TEvent eventData)
    {
        Received.Add(eventData);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 测试替身：与 <see cref="RecordingLocalHandler{TEvent}"/> 行为一致的另一个处理器类型
/// </summary>
/// <typeparam name="TEvent">事件类型</typeparam>
/// <remarks>
/// 处理器工厂按「处理器类型」去重，需要两个不同类型才能验证「同一事件多处理器」。
/// </remarks>
public class AlternateRecordingLocalHandler<TEvent> : ILocalEventHandler<TEvent>
    where TEvent : class
{
    /// <summary>
    /// 按到达顺序记录的事件
    /// </summary>
    public List<TEvent> Received { get; } = [];

    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    public Task HandleEventAsync(TEvent eventData)
    {
        Received.Add(eventData);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 测试替身：总是失败的本地事件处理器
/// </summary>
/// <typeparam name="TEvent">事件类型</typeparam>
public class ThrowingLocalHandler<TEvent> : ILocalEventHandler<TEvent>
    where TEvent : class
{
    /// <summary>
    /// 失败消息，用于在聚合异常里区分是哪个处理器抛的
    /// </summary>
    public string FailureMessage { get; set; } = "本地处理器故意失败";

    /// <summary>
    /// 被调用次数
    /// </summary>
    public int CallCount { get; private set; }

    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>永远失败的任务</returns>
    public Task HandleEventAsync(TEvent eventData)
    {
        CallCount++;
        return Task.FromException(new InvalidOperationException(FailureMessage));
    }
}

/// <summary>
/// 测试替身：记录处理时刻的当前租户
/// </summary>
/// <typeparam name="TEvent">事件类型</typeparam>
public class TenantCapturingHandler<TEvent> : ILocalEventHandler<TEvent>
    where TEvent : class
{
    private readonly ICurrentTenant _currentTenant;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="currentTenant">当前租户上下文</param>
    public TenantCapturingHandler(ICurrentTenant currentTenant)
    {
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// 按到达顺序记录处理时刻的租户唯一标识
    /// </summary>
    public List<long?> CapturedTenantIds { get; } = [];

    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    public Task HandleEventAsync(TEvent eventData)
    {
        CapturedTenantIds.Add(_currentTenant.Id);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 测试替身：把自身类型名写入共享调用轨迹，用于验证处理器执行顺序
/// </summary>
public abstract class TraceRecordingHandler : ILocalEventHandler<PlainNoticeEvent>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="trace">共享调用轨迹</param>
    protected TraceRecordingHandler(List<string> trace)
    {
        Trace = trace;
    }

    /// <summary>
    /// 共享调用轨迹
    /// </summary>
    protected List<string> Trace { get; }

    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    public Task HandleEventAsync(PlainNoticeEvent eventData)
    {
        Trace.Add(GetType().Name);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 测试替身：声明为最先执行的处理器
/// </summary>
[LocalEventHandlerOrder(-10)]
public sealed class EarlyOrderedHandler : TraceRecordingHandler
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="trace">共享调用轨迹</param>
    public EarlyOrderedHandler(List<string> trace) : base(trace)
    {
    }
}

/// <summary>
/// 测试替身：未声明顺序的处理器，顺序应视作 0
/// </summary>
public sealed class DefaultOrderedHandler : TraceRecordingHandler
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="trace">共享调用轨迹</param>
    public DefaultOrderedHandler(List<string> trace) : base(trace)
    {
    }
}

/// <summary>
/// 测试替身：声明为最后执行的处理器
/// </summary>
[LocalEventHandlerOrder(10)]
public sealed class LateOrderedHandler : TraceRecordingHandler
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="trace">共享调用轨迹</param>
    public LateOrderedHandler(List<string> trace) : base(trace)
    {
    }
}

/// <summary>
/// 测试替身：具备无参构造函数的本地事件处理器
/// </summary>
public sealed class ParameterlessLocalHandler : ILocalEventHandler<PlainNoticeEvent>
{
    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    public Task HandleEventAsync(PlainNoticeEvent eventData) => Task.CompletedTask;
}

/// <summary>
/// 测试替身：可释放的本地事件处理器，用于验证瞬时工厂在包装器释放时清理实例
/// </summary>
public sealed class DisposableLocalHandler : ILocalEventHandler<PlainNoticeEvent>, IDisposable
{
    /// <summary>
    /// 是否已释放
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    public Task HandleEventAsync(PlainNoticeEvent eventData) => Task.CompletedTask;

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// 测试替身：作用域内的可释放探针，用于观察 IoC 工厂是否连同作用域一起释放
/// </summary>
public sealed class ScopedProbe : IDisposable
{
    /// <summary>
    /// 是否已释放
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// 测试替身：依赖作用域探针的处理器，只能由容器构造
/// </summary>
public sealed class ProbeAwareLocalHandler : ILocalEventHandler<PlainNoticeEvent>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="probe">作用域探针</param>
    public ProbeAwareLocalHandler(ScopedProbe probe)
    {
        Probe = probe;
    }

    /// <summary>
    /// 作用域探针
    /// </summary>
    public ScopedProbe Probe { get; }

    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    public Task HandleEventAsync(PlainNoticeEvent eventData) => Task.CompletedTask;
}

/// <summary>
/// 测试替身：记录收到的分布式事件
/// </summary>
/// <typeparam name="TEvent">事件类型</typeparam>
public class RecordingDistributedHandler<TEvent> : IDistributedEventHandler<TEvent>
    where TEvent : class
{
    /// <summary>
    /// 按到达顺序记录的事件
    /// </summary>
    public List<TEvent> Received { get; } = [];

    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    public Task HandleEventAsync(TEvent eventData)
    {
        Received.Add(eventData);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 测试替身：与 <see cref="RecordingDistributedHandler{TEvent}"/> 行为一致的另一个分布式处理器类型
/// </summary>
/// <typeparam name="TEvent">事件类型</typeparam>
public class AlternateRecordingDistributedHandler<TEvent> : IDistributedEventHandler<TEvent>
    where TEvent : class
{
    /// <summary>
    /// 按到达顺序记录的事件
    /// </summary>
    public List<TEvent> Received { get; } = [];

    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    public Task HandleEventAsync(TEvent eventData)
    {
        Received.Add(eventData);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 测试替身：总是失败的分布式事件处理器
/// </summary>
/// <typeparam name="TEvent">事件类型</typeparam>
public class ThrowingDistributedHandler<TEvent> : IDistributedEventHandler<TEvent>
    where TEvent : class
{
    /// <summary>
    /// 失败消息
    /// </summary>
    public string FailureMessage { get; set; } = "分布式处理器故意失败";

    /// <summary>
    /// 被调用次数
    /// </summary>
    public int CallCount { get; private set; }

    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>永远失败的任务</returns>
    public Task HandleEventAsync(TEvent eventData)
    {
        CallCount++;
        return Task.FromException(new InvalidOperationException(FailureMessage));
    }
}

/// <summary>
/// 测试替身：同时实现本地与分布式处理器接口，两条通道分别显式实现以便区分
/// </summary>
public sealed class DualChannelHandler : ILocalEventHandler<PlainNoticeEvent>, IDistributedEventHandler<PlainNoticeEvent>
{
    /// <summary>
    /// 本地通道调用次数
    /// </summary>
    public int LocalCallCount { get; private set; }

    /// <summary>
    /// 分布式通道调用次数
    /// </summary>
    public int DistributedCallCount { get; private set; }

    /// <summary>
    /// 本地通道处理
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    Task ILocalEventHandler<PlainNoticeEvent>.HandleEventAsync(PlainNoticeEvent eventData)
    {
        LocalCallCount++;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 分布式通道处理
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    Task IDistributedEventHandler<PlainNoticeEvent>.HandleEventAsync(PlainNoticeEvent eventData)
    {
        DistributedCallCount++;
        return Task.CompletedTask;
    }
}

/// <summary>
/// 测试替身：只实现了标记接口、没有任何处理方法的伪处理器
/// </summary>
public sealed class MarkerOnlyHandler : IEventHandler
{
}

/// <summary>
/// 测试替身：订阅「分布式事件已发送」通知并失败，用于验证通知失败不影响主流程
/// </summary>
public sealed class ThrowingSentObserver : ILocalEventHandler<DistributedEventSent>
{
    /// <summary>
    /// 被调用次数
    /// </summary>
    public int CallCount { get; private set; }

    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>永远失败的任务</returns>
    public Task HandleEventAsync(DistributedEventSent eventData)
    {
        CallCount++;
        return Task.FromException(new InvalidOperationException("发送通知观察者故意失败"));
    }
}

/// <summary>
/// 测试替身：记录「分布式事件已接收」通知
/// </summary>
public sealed class RecordingReceivedObserver : ILocalEventHandler<DistributedEventReceived>
{
    /// <summary>
    /// 按到达顺序记录的通知
    /// </summary>
    public List<DistributedEventReceived> Received { get; } = [];

    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    public Task HandleEventAsync(DistributedEventReceived eventData)
    {
        Received.Add(eventData);
        return Task.CompletedTask;
    }
}

#endregion 事件处理器

#region 事件盒替身

/// <summary>
/// 测试替身：第二个发件箱实现类型，用于验证多发件箱编排
/// </summary>
/// <remarks>
/// 发件箱按 <c>ImplementationType</c> 从容器解析，必须是不同的类型才能同时注册两个。
/// </remarks>
public sealed class SecondaryEventOutbox : InMemoryEventOutbox
{
}

/// <summary>
/// 测试替身：第二个收件箱实现类型，用于验证多收件箱编排
/// </summary>
public sealed class SecondaryEventInbox : InMemoryEventInbox
{
}

#endregion 事件盒替身

#region 装配器

/// <summary>
/// 本地事件总线测试装配器
/// </summary>
/// <remarks>
/// 用真实的 <see cref="LocalEventBus"/> 与 <see cref="EventHandlerInvoker"/> 拼出一条不依赖模块系统的最小链路，
/// 只把租户上下文与工作单元换成可观察的替身。
/// </remarks>
public sealed class LocalEventBusHarness : IDisposable
{
    private readonly ServiceProvider _provider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="provider">服务提供器</param>
    /// <param name="options">本地事件总线选项</param>
    private LocalEventBusHarness(ServiceProvider provider, XiHanLocalEventBusOptions options)
    {
        _provider = provider;
        Options = options;
        CurrentTenant = new FakeCurrentTenant();
        UnitOfWorkManager = new FakeUnitOfWorkManager();
        ServiceScopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        Bus = new LocalEventBus(
            Microsoft.Extensions.Options.Options.Create(options),
            ServiceScopeFactory,
            CurrentTenant,
            UnitOfWorkManager,
            new EventHandlerInvoker());
    }

    /// <summary>
    /// 本地事件总线
    /// </summary>
    public LocalEventBus Bus { get; }

    /// <summary>
    /// 本地事件总线选项
    /// </summary>
    public XiHanLocalEventBusOptions Options { get; }

    /// <summary>
    /// 当前租户上下文
    /// </summary>
    public FakeCurrentTenant CurrentTenant { get; }

    /// <summary>
    /// 工作单元管理器
    /// </summary>
    public FakeUnitOfWorkManager UnitOfWorkManager { get; }

    /// <summary>
    /// 服务作用域工厂
    /// </summary>
    public IServiceScopeFactory ServiceScopeFactory { get; }

    /// <summary>
    /// 服务提供器
    /// </summary>
    public IServiceProvider Services => _provider;

    /// <summary>
    /// 创建装配器
    /// </summary>
    /// <param name="configureServices">额外的服务注册</param>
    /// <param name="configureOptions">选项配置，必须在总线构造前完成</param>
    /// <returns>装配器</returns>
    public static LocalEventBusHarness Create(
        Action<IServiceCollection>? configureServices = null,
        Action<XiHanLocalEventBusOptions>? configureOptions = null)
    {
        var services = new ServiceCollection();
        configureServices?.Invoke(services);
        var provider = services.BuildServiceProvider();

        var options = new XiHanLocalEventBusOptions();
        configureOptions?.Invoke(options);

        return new LocalEventBusHarness(provider, options);
    }

    /// <summary>
    /// 开启一个环境工作单元
    /// </summary>
    /// <returns>工作单元</returns>
    public FakeUnitOfWork StartUnitOfWork()
    {
        var unitOfWork = new FakeUnitOfWork(_provider);
        UnitOfWorkManager.Current = unitOfWork;
        return unitOfWork;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _provider.Dispose();
    }
}

/// <summary>
/// 本地分布式事件总线测试装配器
/// </summary>
/// <remarks>
/// <see cref="LocalDistributedEventBus"/> 把订阅与触发全部委派给本地事件总线，
/// 因此可以在完全不连接消息中间件的前提下验证发件箱/收件箱编排。
/// </remarks>
public sealed class LocalDistributedEventBusHarness : IDisposable
{
    private readonly ServiceProvider _provider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="provider">服务提供器</param>
    /// <param name="localOptions">本地事件总线选项</param>
    /// <param name="distributedOptions">分布式事件总线选项</param>
    /// <param name="correlationId">初始关联标识</param>
    private LocalDistributedEventBusHarness(
        ServiceProvider provider,
        XiHanLocalEventBusOptions localOptions,
        XiHanDistributedEventBusOptions distributedOptions,
        string? correlationId)
    {
        _provider = provider;
        LocalOptions = localOptions;
        DistributedOptions = distributedOptions;
        CurrentTenant = new FakeCurrentTenant();
        UnitOfWorkManager = new FakeUnitOfWorkManager();
        CorrelationIdProvider = new FakeCorrelationIdProvider(correlationId);
        ServiceScopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var invoker = new EventHandlerInvoker();
        LocalBus = new LocalEventBus(
            Microsoft.Extensions.Options.Options.Create(localOptions),
            ServiceScopeFactory,
            CurrentTenant,
            UnitOfWorkManager,
            invoker);

        Bus = new LocalDistributedEventBus(
            ServiceScopeFactory,
            CurrentTenant,
            UnitOfWorkManager,
            Microsoft.Extensions.Options.Options.Create(distributedOptions),
            new StubGuidGenerator(),
            new StubClock(),
            invoker,
            LocalBus,
            CorrelationIdProvider);
    }

    /// <summary>
    /// 分布式事件总线
    /// </summary>
    public LocalDistributedEventBus Bus { get; }

    /// <summary>
    /// 底层本地事件总线
    /// </summary>
    public LocalEventBus LocalBus { get; }

    /// <summary>
    /// 本地事件总线选项
    /// </summary>
    public XiHanLocalEventBusOptions LocalOptions { get; }

    /// <summary>
    /// 分布式事件总线选项
    /// </summary>
    public XiHanDistributedEventBusOptions DistributedOptions { get; }

    /// <summary>
    /// 当前租户上下文
    /// </summary>
    public FakeCurrentTenant CurrentTenant { get; }

    /// <summary>
    /// 工作单元管理器
    /// </summary>
    public FakeUnitOfWorkManager UnitOfWorkManager { get; }

    /// <summary>
    /// 关联标识提供器
    /// </summary>
    public FakeCorrelationIdProvider CorrelationIdProvider { get; }

    /// <summary>
    /// 服务作用域工厂
    /// </summary>
    public IServiceScopeFactory ServiceScopeFactory { get; }

    /// <summary>
    /// 服务提供器
    /// </summary>
    public IServiceProvider Services => _provider;

    /// <summary>
    /// 创建装配器
    /// </summary>
    /// <param name="configureServices">额外的服务注册</param>
    /// <param name="configureOptions">分布式选项配置，必须在总线构造前完成</param>
    /// <param name="correlationId">初始关联标识</param>
    /// <returns>装配器</returns>
    public static LocalDistributedEventBusHarness Create(
        Action<IServiceCollection>? configureServices = null,
        Action<XiHanDistributedEventBusOptions>? configureOptions = null,
        string? correlationId = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<InMemoryEventOutbox>();
        services.AddSingleton<InMemoryEventInbox>();
        services.AddSingleton<SecondaryEventOutbox>();
        services.AddSingleton<SecondaryEventInbox>();
        configureServices?.Invoke(services);
        var provider = services.BuildServiceProvider();

        var distributedOptions = new XiHanDistributedEventBusOptions();
        configureOptions?.Invoke(distributedOptions);

        return new LocalDistributedEventBusHarness(
            provider,
            new XiHanLocalEventBusOptions(),
            distributedOptions,
            correlationId);
    }

    /// <summary>
    /// 开启一个环境工作单元
    /// </summary>
    /// <returns>工作单元</returns>
    public FakeUnitOfWork StartUnitOfWork()
    {
        var unitOfWork = new FakeUnitOfWork(_provider);
        UnitOfWorkManager.Current = unitOfWork;
        return unitOfWork;
    }

    /// <summary>
    /// 获取默认的内存发件箱
    /// </summary>
    /// <returns>发件箱</returns>
    public InMemoryEventOutbox GetOutbox() => _provider.GetRequiredService<InMemoryEventOutbox>();

    /// <summary>
    /// 获取默认的内存收件箱
    /// </summary>
    /// <returns>收件箱</returns>
    public InMemoryEventInbox GetInbox() => _provider.GetRequiredService<InMemoryEventInbox>();

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _provider.Dispose();
    }
}

#endregion 装配器
