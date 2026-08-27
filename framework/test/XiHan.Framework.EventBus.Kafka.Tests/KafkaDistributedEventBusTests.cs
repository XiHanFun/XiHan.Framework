// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.Tracing;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.EventBus.Local;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Timing;

namespace XiHan.Framework.EventBus.Kafka.Tests;

/// <summary>
/// Kafka 分布式事件总线测试
/// </summary>
/// <remarks>
/// 全程不连接 Kafka：发布方向只验证写进消息 Value 的字节，消费方向直接调用消费循环的唯一入口
/// <c>ProcessIncomingMessageAsync</c>，等价于 Broker 刚投递过来一条消息。
/// 需要真实 Broker 的往返验证放在 <see cref="KafkaBrokerRoundTripTests"/>，不可达时整类跳过。
/// </remarks>
public class KafkaDistributedEventBusTests
{
    /// <summary>
    /// 测试事件的事件名，与 <see cref="KafkaTestEvent"/> 上的特性一致
    /// </summary>
    private const string TestEventName = "kafka.test.event";

    /// <summary>
    /// 总线同时以接口和自身类型暴露
    /// </summary>
    /// <remarks>
    /// 缺了接口，宿主注入 <c>IDistributedEventBus</c> 拿到的还是默认本地实现，事件根本没进 Kafka；
    /// 缺了自身类型，模块初始化阶段 <c>GetRequiredService&lt;KafkaDistributedEventBus&gt;()</c> 会直接抛。
    /// </remarks>
    [Fact]
    public void Type_ExposesDistributedEventBusAndItself()
    {
        var attribute = Assert.Single(typeof(KafkaDistributedEventBus).GetCustomAttributes<ExposeServicesAttribute>(false));

        Assert.Equal([typeof(IDistributedEventBus), typeof(KafkaDistributedEventBus)], attribute.ServiceTypes);
        Assert.Equal(
            [typeof(IDistributedEventBus), typeof(KafkaDistributedEventBus)],
            attribute.GetExposedServiceTypes(typeof(KafkaDistributedEventBus)));
    }

    /// <summary>
    /// 总线按单例注册
    /// </summary>
    /// <remarks>
    /// 生产者、消费者与消费循环都挂在实例字段上，非单例会导致每次注入都新建一份连接与消费循环。
    /// </remarks>
    [Fact]
    public void Type_IsRegisteredAsSingletonByConvention()
    {
        Assert.True(typeof(KafkaDistributedEventBus).IsAssignableTo(typeof(ISingletonDependency)));
    }

    /// <summary>
    /// 总线是 Broker 型分布式事件总线，并支持异步释放
    /// </summary>
    [Fact]
    public void Type_IsBrokerDistributedEventBusAndAsyncDisposable()
    {
        Assert.True(typeof(KafkaDistributedEventBus).IsAssignableTo(typeof(BrokerDistributedEventBusBase)));
        Assert.True(typeof(KafkaDistributedEventBus).IsAssignableTo(typeof(IDistributedEventBus)));
        Assert.True(typeof(KafkaDistributedEventBus).IsAssignableTo(typeof(IAsyncDisposable)));
    }

    /// <summary>
    /// 缺少服务作用域工厂时构造失败
    /// </summary>
    [Fact]
    public void Ctor_WhenServiceScopeFactoryIsNull_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => CreateBus(null, new StubCurrentTenant(), new RecordingEventHandlerInvoker()));

        Assert.Equal("serviceScopeFactory", exception.ParamName);
    }

    /// <summary>
    /// 缺少租户访问器时构造失败
    /// </summary>
    [Fact]
    public void Ctor_WhenCurrentTenantIsNull_Throws()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var exception = Assert.Throws<ArgumentNullException>(
            () => CreateBus(provider.GetRequiredService<IServiceScopeFactory>(), null, new RecordingEventHandlerInvoker()));

        Assert.Equal("currentTenant", exception.ParamName);
    }

    /// <summary>
    /// 缺少事件处理器调用器时构造失败
    /// </summary>
    [Fact]
    public void Ctor_WhenEventHandlerInvokerIsNull_Throws()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var exception = Assert.Throws<ArgumentNullException>(
            () => CreateBus(provider.GetRequiredService<IServiceScopeFactory>(), new StubCurrentTenant(), null));

        Assert.Equal("eventHandlerInvoker", exception.ParamName);
    }

    /// <summary>
    /// 序列化结果是不带 BOM 的 UTF-8 JSON，属性名保持 CLR 声明形态
    /// </summary>
    /// <remarks>
    /// 这串字节就是写进 Kafka 消息 Value 的内容，跨语言消费方按它解析；
    /// 一旦改成驼峰或加上 BOM，所有已上线的消费方都会解析失败，因此逐字锁死。
    /// </remarks>
    [Fact]
    public async Task Serialize_ProducesUtf8JsonWithClrPropertyNames()
    {
        await using var host = new KafkaEventBusTestHost();

        var bytes = host.Bus.SerializeForTest(new KafkaTestEvent { Payload = "hello" });

        // 带 BOM 会让解码结果多出一个前导字符，等值断言即可同时锁住编码与属性名两件事
        Assert.Equal("{\"Payload\":\"hello\"}", Encoding.UTF8.GetString(bytes));
    }

    /// <summary>
    /// 非 ASCII 载荷可完整往返
    /// </summary>
    [Fact]
    public async Task Serialize_NonAsciiPayload_RoundTrips()
    {
        await using var host = new KafkaEventBusTestHost();

        var bytes = host.Bus.SerializeForTest(new KafkaTestEvent { Payload = "订单已创建" });
        var restored = JsonSerializer.Deserialize<KafkaTestEvent>(Encoding.UTF8.GetString(bytes));

        Assert.NotNull(restored);
        Assert.Equal("订单已创建", restored.Payload);
    }

    /// <summary>
    /// 收到未订阅事件的消息时直接忽略
    /// </summary>
    /// <remarks>
    /// 同一主题被整个集群共用，本实例必然会收到大量与自己无关的事件。
    /// 这里故意送进一段不是 JSON 的字节：若实现先反序列化再判断订阅，这个用例会抛异常，
    /// 而线上表现就是消费循环被无关消息刷满错误日志。
    /// </remarks>
    [Fact]
    public async Task ProcessIncomingMessage_WhenEventNameNotSubscribed_IsIgnored()
    {
        await using var host = new KafkaEventBusTestHost();

        await host.Bus.ProcessIncomingMessageForTestAsync(
            "message-1",
            "some.other.service.event",
            null,
            Encoding.UTF8.GetBytes("这不是合法的 JSON"));

        Assert.Empty(host.Invoker.Invocations);
    }

    /// <summary>
    /// 收到已订阅事件的消息时反序列化并派发给处理器
    /// </summary>
    [Fact]
    public async Task ProcessIncomingMessage_WhenEventNameSubscribed_InvokesHandlerWithDeserializedData()
    {
        await using var host = new KafkaEventBusTestHost();
        var handler = new KafkaTestEventHandler();
        host.Bus.Subscribe(typeof(KafkaTestEvent), new SingleInstanceHandlerFactory(handler));

        await host.Bus.ProcessIncomingMessageForTestAsync(
            "message-1",
            TestEventName,
            null,
            Encoding.UTF8.GetBytes("{\"Payload\":\"from-broker\"}"));

        var invocation = Assert.Single(host.Invoker.Invocations);

        Assert.Same(handler, invocation.Handler);
        Assert.Equal(typeof(KafkaTestEvent), invocation.EventType);
        Assert.Equal("from-broker", Assert.IsType<KafkaTestEvent>(invocation.EventData).Payload);
    }

    /// <summary>
    /// 路由只认事件名，不认类型全名
    /// </summary>
    /// <remarks>
    /// 事件名来自 <c>EventNameAttribute</c>，发布端把它写进消息 Key、消费端据它查表。
    /// 若哪天两端取名规则不一致，消息会被静默丢弃而不是报错，这里把这条边界显式钉住。
    /// </remarks>
    [Fact]
    public async Task ProcessIncomingMessage_WhenKeyIsTypeFullName_IsIgnored()
    {
        await using var host = new KafkaEventBusTestHost();
        host.Bus.Subscribe(typeof(KafkaTestEvent), new SingleInstanceHandlerFactory(new KafkaTestEventHandler()));

        await host.Bus.ProcessIncomingMessageForTestAsync(
            "message-1",
            typeof(KafkaTestEvent).FullName!,
            null,
            Encoding.UTF8.GetBytes("{\"Payload\":\"from-broker\"}"));

        Assert.Empty(host.Invoker.Invocations);
    }

    /// <summary>
    /// 消息头里的关联标识会被用于切换上下文
    /// </summary>
    /// <remarks>
    /// 关联标识由发布端写进 X-Correlation-Id 消息头，消费端据此把日志与审计归入同一条调用链。
    /// </remarks>
    [Fact]
    public async Task ProcessIncomingMessage_AppliesCorrelationIdFromHeader()
    {
        await using var host = new KafkaEventBusTestHost();
        host.Bus.Subscribe(typeof(KafkaTestEvent), new SingleInstanceHandlerFactory(new KafkaTestEventHandler()));

        await host.Bus.ProcessIncomingMessageForTestAsync(
            "message-1",
            TestEventName,
            "0af7651916cd43dd8448eb211c80319c",
            Encoding.UTF8.GetBytes("{\"Payload\":\"from-broker\"}"));

        Assert.Equal("0af7651916cd43dd8448eb211c80319c", host.CorrelationIdProvider.LastChangedTo);
    }

    /// <summary>
    /// 处理器抛出的异常原样向上传播
    /// </summary>
    /// <remarks>
    /// Kafka 消费循环靠这个异常决定是否记错误日志并提交偏移；
    /// 若在此处被吞掉或被包成聚合异常，毒消息的排查线索会全部丢失。
    /// </remarks>
    [Fact]
    public async Task ProcessIncomingMessage_WhenHandlerThrows_PropagatesOriginalException()
    {
        await using var host = new KafkaEventBusTestHost();
        host.Invoker.OnInvoke = _ => throw new InvalidOperationException("处理失败");
        host.Bus.Subscribe(typeof(KafkaTestEvent), new SingleInstanceHandlerFactory(new KafkaTestEventHandler()));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Bus.ProcessIncomingMessageForTestAsync(
                "message-1",
                TestEventName,
                null,
                Encoding.UTF8.GetBytes("{\"Payload\":\"from-broker\"}")));

        Assert.Equal("处理失败", exception.Message);
    }

    /// <summary>
    /// 订阅登记到本地事件总线
    /// </summary>
    /// <remarks>
    /// 分布式总线自己不维护处理器表，派发完全委派给本地事件总线；这条委派断了，消息收到也无人处理。
    /// </remarks>
    [Fact]
    public async Task Subscribe_DelegatesRegistrationToLocalEventBus()
    {
        await using var host = new KafkaEventBusTestHost();
        var factory = new SingleInstanceHandlerFactory(new KafkaTestEventHandler());

        var subscription = host.Bus.Subscribe(typeof(KafkaTestEvent), factory);

        Assert.NotNull(subscription);
        Assert.Contains(
            host.LocalBus.GetEventHandlerFactories(typeof(KafkaTestEvent)),
            item => item.EventHandlerFactories.Contains(factory));
    }

    /// <summary>
    /// 取消全部订阅后本地事件总线不再持有处理器
    /// </summary>
    [Fact]
    public async Task UnsubscribeAll_ClearsLocalEventBusRegistrations()
    {
        await using var host = new KafkaEventBusTestHost();
        host.Bus.Subscribe(typeof(KafkaTestEvent), new SingleInstanceHandlerFactory(new KafkaTestEventHandler()));

        host.Bus.UnsubscribeAll(typeof(KafkaTestEvent));

        Assert.Empty(host.LocalBus.GetEventHandlerFactories(typeof(KafkaTestEvent)));
    }

    /// <summary>
    /// 取消订阅后消息不再派发
    /// </summary>
    [Fact]
    public async Task ProcessIncomingMessage_AfterUnsubscribeAll_DoesNotInvokeHandler()
    {
        await using var host = new KafkaEventBusTestHost();
        host.Bus.Subscribe(typeof(KafkaTestEvent), new SingleInstanceHandlerFactory(new KafkaTestEventHandler()));
        host.Bus.UnsubscribeAll(typeof(KafkaTestEvent));

        await host.Bus.ProcessIncomingMessageForTestAsync(
            "message-1",
            TestEventName,
            null,
            Encoding.UTF8.GetBytes("{\"Payload\":\"from-broker\"}"));

        Assert.Empty(host.Invoker.Invocations);
    }

    /// <summary>
    /// 从未初始化过也能安静释放
    /// </summary>
    /// <remarks>
    /// 宿主可能在连接建立前就关停（配置错误、启动失败等）。此时生产者、消费者、消费任务全为空，
    /// 释放必须直接完成；实现里任何空引用或无限等待都会卡住宿主关闭流程，故加超时保护。
    /// </remarks>
    [Fact]
    public async Task DisposeAsync_WhenNeverInitialized_CompletesQuietly()
    {
        var host = new KafkaEventBusTestHost();

        await host.Bus.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await host.DisposeAsync();
    }

    /// <summary>
    /// 重复释放是幂等的
    /// </summary>
    /// <remarks>
    /// 容器释放与宿主显式释放可能同时发生，第二次调用不允许抛异常。
    /// </remarks>
    [Fact]
    public async Task DisposeAsync_CalledTwice_IsIdempotent()
    {
        var host = new KafkaEventBusTestHost();

        await host.Bus.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await host.Bus.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await host.DisposeAsync();
    }

    /// <summary>
    /// 构造函数的依赖可由真实容器满足，且两个暴露服务解析到同一单例
    /// </summary>
    /// <remarks>
    /// 这是约定注册在容器里的等价形态。同时也确认构造过程不接触 Broker——
    /// 若构造函数里就建连接，这个用例会在没有 Kafka 的机器上失败或挂住。
    /// </remarks>
    [Fact]
    public async Task Registration_WhenExposedServicesRegistered_ResolveToTheSameSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentTenant, StubCurrentTenant>();
        services.AddSingleton<XiHan.Framework.Uow.IUnitOfWorkManager, StubUnitOfWorkManager>();
        services.AddSingleton<IEventHandlerInvoker, RecordingEventHandlerInvoker>();
        services.AddSingleton<IDistributedIdGenerator<Guid>, StubGuidGenerator>();
        services.AddSingleton<IClock, StubClock>();
        services.AddSingleton<ICorrelationIdProvider, StubCorrelationIdProvider>();
        services.AddSingleton<IOptions<XiHanDistributedEventBusOptions>>(Options.Create(new XiHanDistributedEventBusOptions()));
        services.AddSingleton<IOptions<XiHanKafkaEventBusOptions>>(Options.Create(new XiHanKafkaEventBusOptions()));
        services.AddSingleton<ILogger<KafkaDistributedEventBus>>(NullLogger<KafkaDistributedEventBus>.Instance);
        services.AddSingleton<ILocalEventBus>(sp => new LocalEventBus(
            Options.Create(new XiHanLocalEventBusOptions()),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ICurrentTenant>(),
            sp.GetRequiredService<XiHan.Framework.Uow.IUnitOfWorkManager>(),
            sp.GetRequiredService<IEventHandlerInvoker>()));

        services.AddSingleton<KafkaDistributedEventBus>();
        services.AddSingleton<IDistributedEventBus>(sp => sp.GetRequiredService<KafkaDistributedEventBus>());

        await using var provider = services.BuildServiceProvider();

        var concrete = provider.GetRequiredService<KafkaDistributedEventBus>();

        Assert.Same(concrete, provider.GetRequiredService<IDistributedEventBus>());
        Assert.Same(concrete, provider.GetRequiredService<KafkaDistributedEventBus>());
    }

    /// <summary>
    /// 构造 Kafka 事件总线，允许把任一依赖替换为空以验证参数校验
    /// </summary>
    /// <param name="serviceScopeFactory">服务作用域工厂</param>
    /// <param name="currentTenant">租户访问器</param>
    /// <param name="eventHandlerInvoker">事件处理器调用器</param>
    /// <returns>事件总线</returns>
    private static KafkaDistributedEventBus CreateBus(
        IServiceScopeFactory? serviceScopeFactory,
        ICurrentTenant? currentTenant,
        IEventHandlerInvoker? eventHandlerInvoker)
    {
        return new KafkaDistributedEventBus(
            serviceScopeFactory!,
            currentTenant!,
            new StubUnitOfWorkManager(),
            Options.Create(new XiHanDistributedEventBusOptions()),
            Options.Create(new XiHanKafkaEventBusOptions()),
            new StubGuidGenerator(),
            new StubClock(),
            eventHandlerInvoker!,
            NullLocalEventBus.Instance,
            new StubCorrelationIdProvider(),
            NullLogger<KafkaDistributedEventBus>.Instance);
    }
}
