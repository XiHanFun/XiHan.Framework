// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Distributed;

namespace XiHan.Framework.EventBus.RabbitMQ.Tests;

/// <summary>
/// RabbitMQ 分布式事件总线测试
/// </summary>
/// <remarks>
/// 这一组只覆盖不依赖真实连接的逻辑：装配契约、路由键推导、载荷编码、订阅委派与消费分发。
/// 需要真实 Broker 才有意义的往返验证放在 <see cref="RabbitMQBrokerRoundTripTests"/>，不可达时整组跳过。
/// </remarks>
public class RabbitMQDistributedEventBusTests
{
    /// <summary>
    /// 同时暴露分布式事件总线接口与自身类型
    /// </summary>
    /// <remarks>
    /// 接口用于替换默认实现，自身类型用于模块在应用初始化阶段取到实例调用 <c>InitializeAsync</c>；
    /// 少暴露任何一个都会让启动路径断掉。
    /// </remarks>
    [Fact]
    public void Type_ExposesDistributedEventBusAndSelf()
    {
        var attribute = typeof(RabbitMQDistributedEventBus).GetCustomAttribute<ExposeServicesAttribute>(false);

        Assert.NotNull(attribute);
        Assert.Equal(2, attribute.ServiceTypes.Length);
        Assert.Contains(typeof(IDistributedEventBus), attribute.ServiceTypes);
        Assert.Contains(typeof(RabbitMQDistributedEventBus), attribute.ServiceTypes);
    }

    /// <summary>
    /// 以单例生命周期注册
    /// </summary>
    /// <remarks>
    /// 连接、通道与消费者都挂在实例上，一旦退化成瞬时或作用域，每次解析都会重建连接并重复消费。
    /// </remarks>
    [Fact]
    public void Type_IsSingletonDependency()
    {
        Assert.True(typeof(RabbitMQDistributedEventBus).IsAssignableTo(typeof(ISingletonDependency)));
    }

    /// <summary>
    /// 复用 Broker 事件总线基类并支持异步释放
    /// </summary>
    [Fact]
    public void Type_IsBrokerEventBusAndAsyncDisposable()
    {
        Assert.True(typeof(RabbitMQDistributedEventBus).IsAssignableTo(typeof(BrokerDistributedEventBusBase)));
        Assert.True(typeof(RabbitMQDistributedEventBus).IsAssignableTo(typeof(IAsyncDisposable)));
    }

    /// <summary>
    /// 构造时缺少服务作用域工厂直接抛出
    /// </summary>
    [Fact]
    public void Constructor_WithNullServiceScopeFactory_Throws()
    {
        using var context = new RabbitMQEventBusTestContext();

        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new TestableRabbitMQEventBus(
                null!,
                Options.Create(context.DistributedOptions),
                Options.Create(context.RabbitMqOptions),
                context.Invoker,
                context.LocalEventBus,
                context.CorrelationIdProvider);
        });

        Assert.Equal("serviceScopeFactory", exception.ParamName);
    }

    /// <summary>
    /// 构造时缺少事件处理器调用器直接抛出
    /// </summary>
    [Fact]
    public void Constructor_WithNullEventHandlerInvoker_Throws()
    {
        using var context = new RabbitMQEventBusTestContext();

        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new TestableRabbitMQEventBus(
                context.Provider.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(context.DistributedOptions),
                Options.Create(context.RabbitMqOptions),
                null!,
                context.LocalEventBus,
                context.CorrelationIdProvider);
        });

        Assert.Equal("eventHandlerInvoker", exception.ParamName);
    }

    /// <summary>
    /// 没有声明处理器时构造不产生任何订阅
    /// </summary>
    [Fact]
    public void Constructor_WithoutDeclaredHandlers_SubscribesNothing()
    {
        using var context = new RabbitMQEventBusTestContext();

        Assert.Empty(context.LocalEventBus.Subscriptions);
        Assert.Empty(context.Bus.RegisteredEventTypes);
    }

    /// <summary>
    /// 构造时把已声明的分布式处理器登记到本地事件总线并填充路由键映射
    /// </summary>
    /// <remarks>
    /// 初始化阶段按 <c>EventTypes.Keys</c> 逐个 QueueBind，这里的映射没填上，
    /// 对应事件的路由键就不会绑定到队列，事件会被交换机直接丢弃。
    /// </remarks>
    [Fact]
    public void Constructor_WithDeclaredHandlers_RegistersRoutingKeys()
    {
        var distributedOptions = new XiHanDistributedEventBusOptions();
        distributedOptions.Handlers.Add<RabbitMQTestEventHandler>();

        using var context = new RabbitMQEventBusTestContext(distributedOptions: distributedOptions);

        var subscription = Assert.Single(context.LocalEventBus.Subscriptions);
        Assert.Equal(typeof(RabbitMQTestEvent), subscription.EventType);

        var registered = Assert.Single(context.Bus.RegisteredEventTypes);
        Assert.Equal(typeof(RabbitMQTestEvent).FullName, registered.Key);
        Assert.Equal(typeof(RabbitMQTestEvent), registered.Value);
    }

    /// <summary>
    /// 未标注事件名时路由键退化为事件类型全名
    /// </summary>
    [Fact]
    public void Subscribe_WithoutEventNameAttribute_UsesTypeFullNameAsRoutingKey()
    {
        using var context = new RabbitMQEventBusTestContext();

        context.Bus.Subscribe(typeof(RabbitMQTestEvent), new StubEventHandlerFactory(new RabbitMQTestEventHandler()));

        Assert.True(context.Bus.RegisteredEventTypes.ContainsKey(typeof(RabbitMQTestEvent).FullName!));
    }

    /// <summary>
    /// 标注事件名时路由键取标注值
    /// </summary>
    /// <remarks>
    /// 路由键是跨服务契约：生产者与消费者只有取到同一个字符串才能对上，
    /// 因此标注值必须原样成为绑定键，不能被类型全名覆盖。
    /// </remarks>
    [Fact]
    public void Subscribe_WithEventNameAttribute_UsesDeclaredRoutingKey()
    {
        using var context = new RabbitMQEventBusTestContext();

        context.Bus.Subscribe(typeof(RabbitMQNamedTestEvent), new StubEventHandlerFactory(new RabbitMQTestEventHandler()));

        Assert.True(context.Bus.RegisteredEventTypes.ContainsKey(RabbitMQNamedTestEvent.RoutingKey));
        Assert.False(context.Bus.RegisteredEventTypes.ContainsKey(typeof(RabbitMQNamedTestEvent).FullName!));
    }

    /// <summary>
    /// 重复订阅同一事件类型只保留一个路由键
    /// </summary>
    [Fact]
    public void Subscribe_Twice_KeepsSingleRoutingKey()
    {
        using var context = new RabbitMQEventBusTestContext();
        var handler = new RabbitMQTestEventHandler();

        context.Bus.Subscribe(typeof(RabbitMQTestEvent), new StubEventHandlerFactory(handler));
        context.Bus.Subscribe(typeof(RabbitMQTestEvent), new StubEventHandlerFactory(handler));

        Assert.Single(context.Bus.RegisteredEventTypes);
        Assert.Equal(2, context.LocalEventBus.Subscriptions.Count);
    }

    /// <summary>
    /// 订阅句柄原样来自本地事件总线
    /// </summary>
    [Fact]
    public void Subscribe_ReturnsLocalEventBusToken()
    {
        using var context = new RabbitMQEventBusTestContext();

        var token = context.Bus.Subscribe(typeof(RabbitMQTestEvent), new StubEventHandlerFactory(new RabbitMQTestEventHandler()));

        Assert.Same(context.LocalEventBus.SubscriptionToken, token);
    }

    /// <summary>
    /// 按工厂退订委派给本地事件总线
    /// </summary>
    [Fact]
    public void Unsubscribe_ByFactory_DelegatesToLocalEventBus()
    {
        using var context = new RabbitMQEventBusTestContext();
        var factory = new StubEventHandlerFactory(new RabbitMQTestEventHandler());

        context.Bus.Unsubscribe(typeof(RabbitMQTestEvent), factory);

        var record = Assert.Single(context.LocalEventBus.Unsubscriptions);
        Assert.Equal(typeof(RabbitMQTestEvent), record.EventType);
        Assert.Same(factory, record.Target);
    }

    /// <summary>
    /// 按处理器实例退订委派给本地事件总线
    /// </summary>
    [Fact]
    public void Unsubscribe_ByHandler_DelegatesToLocalEventBus()
    {
        using var context = new RabbitMQEventBusTestContext();
        var handler = new RabbitMQTestEventHandler();

        context.Bus.Unsubscribe(typeof(RabbitMQTestEvent), handler);

        var record = Assert.Single(context.LocalEventBus.Unsubscriptions);
        Assert.Same(handler, record.Target);
    }

    /// <summary>
    /// 按委托退订委派给本地事件总线
    /// </summary>
    [Fact]
    public void Unsubscribe_ByAction_DelegatesToLocalEventBus()
    {
        using var context = new RabbitMQEventBusTestContext();

        context.Bus.Unsubscribe<RabbitMQTestEvent>(_ => Task.CompletedTask);

        var record = Assert.Single(context.LocalEventBus.Unsubscriptions);
        Assert.Equal(typeof(RabbitMQTestEvent), record.EventType);
    }

    /// <summary>
    /// 退订某事件类型的全部处理器委派给本地事件总线
    /// </summary>
    [Fact]
    public void UnsubscribeAll_DelegatesToLocalEventBus()
    {
        using var context = new RabbitMQEventBusTestContext();

        context.Bus.UnsubscribeAll(typeof(RabbitMQTestEvent));

        Assert.Equal(typeof(RabbitMQTestEvent), Assert.Single(context.LocalEventBus.UnsubscribeAllCalls));
    }

    /// <summary>
    /// 退订不会删除已登记的路由键
    /// </summary>
    /// <remarks>
    /// 路由键映射同时用于入站消息的反序列化，退订后残留是有意的：
    /// 队列绑定在 Broker 侧，重启前不会跟着变。这里把现状固定下来，避免被误当成泄漏顺手"修掉"。
    /// </remarks>
    [Fact]
    public void UnsubscribeAll_KeepsRegisteredRoutingKey()
    {
        using var context = new RabbitMQEventBusTestContext();
        context.Bus.Subscribe(typeof(RabbitMQTestEvent), new StubEventHandlerFactory(new RabbitMQTestEventHandler()));

        context.Bus.UnsubscribeAll(typeof(RabbitMQTestEvent));

        Assert.Single(context.Bus.RegisteredEventTypes);
    }

    /// <summary>
    /// 事件载荷序列化为 UTF-8 JSON
    /// </summary>
    /// <remarks>
    /// 消费端按 UTF-8 JSON 反序列化，编码一旦换成别的（如带 BOM 或 ASCII 转义外的字符集），
    /// 非 ASCII 载荷会在跨语言消费方变成乱码，所以显式用中文载荷验证字节层。
    /// </remarks>
    [Fact]
    public void Serialize_ProducesUtf8Json()
    {
        using var context = new RabbitMQEventBusTestContext();

        var bytes = context.Bus.SerializeForTest(new RabbitMQTestEvent { Payload = "曦寒" });
        var json = Encoding.UTF8.GetString(bytes);

        using var document = JsonDocument.Parse(json);
        Assert.Equal("曦寒", document.RootElement.GetProperty("Payload").GetString());
    }

    /// <summary>
    /// 序列化按运行时类型输出属性，而不是按 object 输出空对象
    /// </summary>
    [Fact]
    public void Serialize_UsesRuntimeType()
    {
        using var context = new RabbitMQEventBusTestContext();

        object eventData = new RabbitMQTestEvent { Payload = "runtime" };
        var json = Encoding.UTF8.GetString(context.Bus.SerializeForTest(eventData));

        Assert.Contains("\"Payload\"", json);
    }

    /// <summary>
    /// 序列化结果能被入站路径原样还原
    /// </summary>
    [Fact]
    public async Task Serialize_RoundTripsThroughIncomingMessage()
    {
        using var context = new RabbitMQEventBusTestContext();
        var handler = new RabbitMQTestEventHandler();
        context.Bus.Subscribe(typeof(RabbitMQTestEvent), new StubEventHandlerFactory(handler));

        var body = context.Bus.SerializeForTest(new RabbitMQTestEvent { Payload = "往返" });

        await context.Bus.ProcessIncomingMessageForTestAsync(
            "message-1",
            typeof(RabbitMQTestEvent).FullName!,
            null,
            body);

        var invocation = Assert.Single(context.Invoker.Invocations);
        var received = Assert.IsType<RabbitMQTestEvent>(invocation.EventData);
        Assert.Equal("往返", received.Payload);
        Assert.Equal(typeof(RabbitMQTestEvent), invocation.EventType);
        Assert.Same(handler, invocation.Handler);
    }

    /// <summary>
    /// 本实例没有订阅的事件名被直接忽略
    /// </summary>
    /// <remarks>
    /// 同一队列上会出现别的实例订阅的事件（共享队列 + 多路由键绑定），
    /// 这类消息必须安静地跳过并由调用方 Ack，否则会被反复 Nack 成毒消息。
    /// </remarks>
    [Fact]
    public async Task ProcessIncomingMessage_WithUnknownEventName_IsIgnored()
    {
        using var context = new RabbitMQEventBusTestContext();

        await context.Bus.ProcessIncomingMessageForTestAsync(
            "message-1",
            "some.other.service.Event",
            null,
            Encoding.UTF8.GetBytes("{\"Payload\":\"x\"}"));

        Assert.Empty(context.Invoker.Invocations);
        Assert.Empty(context.LocalEventBus.PublishedEvents);
    }

    /// <summary>
    /// 入站消息按标注的事件名找回事件类型
    /// </summary>
    [Fact]
    public async Task ProcessIncomingMessage_WithDeclaredEventName_ResolvesEventType()
    {
        using var context = new RabbitMQEventBusTestContext();
        context.Bus.Subscribe(typeof(RabbitMQNamedTestEvent), new StubEventHandlerFactory(new RabbitMQTestEventHandler()));

        var body = context.Bus.SerializeForTest(new RabbitMQNamedTestEvent { Payload = "named" });

        await context.Bus.ProcessIncomingMessageForTestAsync("message-1", RabbitMQNamedTestEvent.RoutingKey, null, body);

        var invocation = Assert.Single(context.Invoker.Invocations);
        Assert.Equal(typeof(RabbitMQNamedTestEvent), invocation.EventType);
    }

    /// <summary>
    /// 入站消息把关联标识切换进当前上下文
    /// </summary>
    /// <remarks>
    /// 消费端日志与审计靠这个标识和上游归到同一条链路，丢了就查不到跨服务因果。
    /// </remarks>
    [Fact]
    public async Task ProcessIncomingMessage_AppliesCorrelationId()
    {
        using var context = new RabbitMQEventBusTestContext();
        context.Bus.Subscribe(typeof(RabbitMQTestEvent), new StubEventHandlerFactory(new RabbitMQTestEventHandler()));

        var body = context.Bus.SerializeForTest(new RabbitMQTestEvent { Payload = "x" });

        await context.Bus.ProcessIncomingMessageForTestAsync("message-1", typeof(RabbitMQTestEvent).FullName!, "correlation-1", body);

        Assert.Contains(context.CorrelationIdProvider.Changes, value => value == "correlation-1");
    }

    /// <summary>
    /// 入站消息触发「已接收」本地通知，来源标记为收件箱路径
    /// </summary>
    [Fact]
    public async Task ProcessIncomingMessage_RaisesReceivedNotification()
    {
        using var context = new RabbitMQEventBusTestContext();
        context.Bus.Subscribe(typeof(RabbitMQTestEvent), new StubEventHandlerFactory(new RabbitMQTestEventHandler()));

        var body = context.Bus.SerializeForTest(new RabbitMQTestEvent { Payload = "x" });

        await context.Bus.ProcessIncomingMessageForTestAsync("message-1", typeof(RabbitMQTestEvent).FullName!, null, body);

        var notification = Assert.IsType<DistributedEventReceived>(Assert.Single(context.LocalEventBus.PublishedEvents));
        Assert.Equal(DistributedEventSource.Inbox, notification.Source);
        Assert.Equal(typeof(RabbitMQTestEvent).FullName, notification.EventName);
    }

    /// <summary>
    /// 处理器抛出的异常原样传播，供消费者决定 Ack 还是 Nack
    /// </summary>
    /// <remarks>
    /// 消费回调靠这个异常判断投递失败并 Nack；若被吞掉，失败消息会被误 Ack 而永久丢失。
    /// </remarks>
    [Fact]
    public async Task ProcessIncomingMessage_WhenHandlerFails_PropagatesOriginalException()
    {
        using var context = new RabbitMQEventBusTestContext();
        context.Invoker.FailWith = new InvalidOperationException("处理失败");
        context.Bus.Subscribe(typeof(RabbitMQTestEvent), new StubEventHandlerFactory(new RabbitMQTestEventHandler()));

        var body = context.Bus.SerializeForTest(new RabbitMQTestEvent { Payload = "x" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Bus.ProcessIncomingMessageForTestAsync("message-1", typeof(RabbitMQTestEvent).FullName!, null, body));

        Assert.Equal("处理失败", exception.Message);
    }

    /// <summary>
    /// 处理器包装在调用结束后被释放
    /// </summary>
    [Fact]
    public async Task ProcessIncomingMessage_DisposesHandlerWrapper()
    {
        using var context = new RabbitMQEventBusTestContext();
        var factory = new StubEventHandlerFactory(new RabbitMQTestEventHandler());
        context.Bus.Subscribe(typeof(RabbitMQTestEvent), factory);

        var body = context.Bus.SerializeForTest(new RabbitMQTestEvent { Payload = "x" });

        await context.Bus.ProcessIncomingMessageForTestAsync("message-1", typeof(RabbitMQTestEvent).FullName!, null, body);

        Assert.Equal(1, factory.DisposeCount);
    }

    /// <summary>
    /// 收件箱里事件名对不上的记录被跳过
    /// </summary>
    [Fact]
    public async Task ProcessFromInbox_WithUnknownEventName_IsIgnored()
    {
        using var context = new RabbitMQEventBusTestContext();

        var incoming = new IncomingEventInfo(
            Guid.NewGuid(),
            "message-1",
            "some.other.service.Event",
            Encoding.UTF8.GetBytes("{\"Payload\":\"x\"}"),
            DateTime.UtcNow);

        await context.Bus.ProcessFromInboxAsync(incoming, new InboxConfig("test"));

        Assert.Empty(context.Invoker.Invocations);
    }

    /// <summary>
    /// 收件箱里已登记的事件被还原并投递给处理器
    /// </summary>
    [Fact]
    public async Task ProcessFromInbox_WithRegisteredEventName_InvokesHandler()
    {
        using var context = new RabbitMQEventBusTestContext();
        context.Bus.Subscribe(typeof(RabbitMQTestEvent), new StubEventHandlerFactory(new RabbitMQTestEventHandler()));

        var incoming = new IncomingEventInfo(
            Guid.NewGuid(),
            "message-1",
            typeof(RabbitMQTestEvent).FullName!,
            context.Bus.SerializeForTest(new RabbitMQTestEvent { Payload = "inbox" }),
            DateTime.UtcNow);

        await context.Bus.ProcessFromInboxAsync(incoming, new InboxConfig("test"));

        var invocation = Assert.Single(context.Invoker.Invocations);
        Assert.Equal("inbox", Assert.IsType<RabbitMQTestEvent>(invocation.EventData).Payload);
    }

    /// <summary>
    /// 连接串写错时初始化直接抛出，不进入连接阶段
    /// </summary>
    /// <remarks>
    /// 这条不需要 Broker：连接串在建连之前就要被解析，格式错误必须当场暴露，
    /// 否则会退化成「连了默认 localhost」的静默错连。
    /// </remarks>
    [Fact]
    public async Task InitializeAsync_WithMalformedUri_Throws()
    {
        using var context = new RabbitMQEventBusTestContext(CreateMalformedUriOptions());

        await Assert.ThrowsAnyAsync<FormatException>(() => context.Bus.InitializeAsync());
    }

    /// <summary>
    /// 初始化失败后重试仍然抛出，不会被误标记为已初始化
    /// </summary>
    [Fact]
    public async Task InitializeAsync_AfterFailure_StillThrows()
    {
        using var context = new RabbitMQEventBusTestContext(CreateMalformedUriOptions());

        await Assert.ThrowsAnyAsync<FormatException>(() => context.Bus.InitializeAsync());
        await Assert.ThrowsAnyAsync<FormatException>(() => context.Bus.InitializeAsync());
    }

    /// <summary>
    /// 未初始化时发布会先尝试初始化，失败则抛出而不是静默丢事件
    /// </summary>
    [Fact]
    public async Task PublishToBroker_WhenInitializationFails_Throws()
    {
        using var context = new RabbitMQEventBusTestContext(CreateMalformedUriOptions());

        await Assert.ThrowsAnyAsync<FormatException>(() => context.Bus.PublishToBrokerForTestAsync(
            typeof(RabbitMQTestEvent).FullName!,
            context.Bus.SerializeForTest(new RabbitMQTestEvent { Payload = "x" }),
            "message-1",
            "correlation-1"));
    }

    /// <summary>
    /// 发件箱投递失败时异常向上传播，交由发件箱重试
    /// </summary>
    [Fact]
    public async Task PublishFromOutbox_WhenInitializationFails_Throws()
    {
        using var context = new RabbitMQEventBusTestContext(CreateMalformedUriOptions());

        var outgoing = new OutgoingEventInfo(
            Guid.NewGuid(),
            typeof(RabbitMQTestEvent).FullName!,
            context.Bus.SerializeForTest(new RabbitMQTestEvent { Payload = "x" }),
            DateTime.UtcNow);

        await Assert.ThrowsAnyAsync<FormatException>(
            () => context.Bus.PublishFromOutboxAsync(outgoing, new OutboxConfig("test")));
    }

    /// <summary>
    /// 从未初始化过也能安全释放
    /// </summary>
    /// <remarks>
    /// 模块注册即单例，若应用在初始化前就关停（配置校验失败、启动被取消），
    /// 释放路径会在连接与通道全为空的状态下被调用，此时不允许抛。
    /// </remarks>
    [Fact]
    public async Task DisposeAsync_WithoutInitialization_DoesNotThrow()
    {
        var context = new RabbitMQEventBusTestContext();
        try
        {
            var exception = await Record.ExceptionAsync(() => context.Bus.DisposeAsync().AsTask());

            Assert.Null(exception);
        }
        finally
        {
            context.Dispose();
        }
    }

    /// <summary>
    /// 初始化失败后仍能安全释放
    /// </summary>
    [Fact]
    public async Task DisposeAsync_AfterFailedInitialization_DoesNotThrow()
    {
        var context = new RabbitMQEventBusTestContext(CreateMalformedUriOptions());
        try
        {
            await Assert.ThrowsAnyAsync<FormatException>(() => context.Bus.InitializeAsync());

            var exception = await Record.ExceptionAsync(() => context.Bus.DisposeAsync().AsTask());

            Assert.Null(exception);
        }
        finally
        {
            context.Dispose();
        }
    }

    /// <summary>
    /// 构造带非法连接串的选项
    /// </summary>
    /// <returns>选项</returns>
    private static XiHanRabbitMQEventBusOptions CreateMalformedUriOptions()
    {
        return new XiHanRabbitMQEventBusOptions
        {
            // 非法连接串在 new Uri 阶段就会失败，因此整条初始化路径不会触碰网络
            Uri = "not a valid uri"
        };
    }
}
