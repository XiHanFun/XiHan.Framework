// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Local;
using XiHan.Framework.EventBus.Tests.Fakes;

namespace XiHan.Framework.EventBus.Tests.Local;

/// <summary>
/// 本地事件总线发布与触发测试
/// </summary>
/// <remarks>
/// 覆盖发布链路的核心契约：多处理器全部被调用、单个处理器失败不吞掉其他处理器、
/// 顺序特性生效、按继承关系触发、以及工作单元在场时的延迟发布。
/// </remarks>
public class LocalEventBusPublishTests
{
    /// <summary>
    /// 发布事件后订阅的处理器拿到同一个事件实例
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithSingleHandler_DeliversSameEventInstance()
    {
        using var harness = LocalEventBusHarness.Create();
        var handler = new RecordingLocalHandler<PlainNoticeEvent>();
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), handler);
        var eventData = new PlainNoticeEvent { Message = "订单已创建" };

        await harness.Bus.PublishAsync(eventData);

        Assert.Same(eventData, Assert.Single(handler.Received));
    }

    /// <summary>
    /// 同一事件的多个处理器全部被调用
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithMultipleHandlers_InvokesEveryHandler()
    {
        using var harness = LocalEventBusHarness.Create();
        var first = new RecordingLocalHandler<PlainNoticeEvent>();
        var second = new AlternateRecordingLocalHandler<PlainNoticeEvent>();
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), first);
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), second);

        await harness.Bus.PublishAsync(new PlainNoticeEvent { Message = "广播" });

        Assert.Single(first.Received);
        Assert.Single(second.Received);
    }

    /// <summary>
    /// 某个处理器抛异常不影响其余处理器被调用
    /// </summary>
    /// <remarks>
    /// 这是异常隔离的核心：失败的处理器只应把异常收集起来，绝不能提前中断整条触发链。
    /// </remarks>
    [Fact]
    public async Task PublishAsync_WhenOneHandlerThrows_StillInvokesRemainingHandlers()
    {
        using var harness = LocalEventBusHarness.Create();
        var before = new RecordingLocalHandler<PlainNoticeEvent>();
        var failing = new ThrowingLocalHandler<PlainNoticeEvent>();
        var after = new AlternateRecordingLocalHandler<PlainNoticeEvent>();
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), before);
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), failing);
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), after);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Bus.PublishAsync(new PlainNoticeEvent { Message = "隔离" }));

        Assert.Single(before.Received);
        Assert.Equal(1, failing.CallCount);
        Assert.Single(after.Received);
    }

    /// <summary>
    /// 只有一个处理器失败时抛出原始异常而不是聚合异常
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenSingleHandlerThrows_RethrowsOriginalException()
    {
        using var harness = LocalEventBusHarness.Create();
        var failing = new ThrowingLocalHandler<PlainNoticeEvent> { FailureMessage = "唯一失败" };
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), failing);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Bus.PublishAsync(new PlainNoticeEvent()));

        Assert.Equal("唯一失败", exception.Message);
    }

    /// <summary>
    /// 多个处理器失败时抛出聚合异常并保留每一个原始异常
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenMultipleHandlersThrow_ThrowsAggregateWithAllFailures()
    {
        using var harness = LocalEventBusHarness.Create();
        var first = new ThrowingLocalHandler<PlainNoticeEvent> { FailureMessage = "第一处失败" };
        var second = new ThrowingLocalHandler<PlainNoticeEvent> { FailureMessage = "第二处失败" };
        var survivor = new RecordingLocalHandler<PlainNoticeEvent>();
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), first);
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), second);
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), survivor);

        var exception = await Assert.ThrowsAsync<AggregateException>(
            () => harness.Bus.PublishAsync(new PlainNoticeEvent()));

        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.Contains(exception.InnerExceptions, item => item.Message == "第一处失败");
        Assert.Contains(exception.InnerExceptions, item => item.Message == "第二处失败");
        Assert.Single(survivor.Received);
    }

    /// <summary>
    /// 处理器按顺序特性声明的次序执行，未声明的视作 0
    /// </summary>
    [Fact]
    public async Task PublishAsync_OrdersHandlersByOrderAttribute()
    {
        using var harness = LocalEventBusHarness.Create();
        var trace = new List<string>();
        // 故意按与期望完全相反的次序订阅，确保断言检验的是排序而不是订阅顺序
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), new LateOrderedHandler(trace));
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), new DefaultOrderedHandler(trace));
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), new EarlyOrderedHandler(trace));

        await harness.Bus.PublishAsync(new PlainNoticeEvent());

        Assert.Equal(
            new[] { nameof(EarlyOrderedHandler), nameof(DefaultOrderedHandler), nameof(LateOrderedHandler) },
            trace);
    }

    /// <summary>
    /// 发布派生事件会触发基类事件的处理器
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithDerivedEvent_InvokesBaseTypeHandler()
    {
        using var harness = LocalEventBusHarness.Create();
        var handler = new RecordingLocalHandler<PlainNoticeEvent>();
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), handler);

        await harness.Bus.PublishAsync(new DerivedNoticeEvent { Message = "派生" });

        Assert.Single(handler.Received);
    }

    /// <summary>
    /// 发布基类事件不会触发只订阅派生事件的处理器
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithBaseEvent_DoesNotInvokeDerivedTypeHandler()
    {
        using var harness = LocalEventBusHarness.Create();
        var handler = new RecordingLocalHandler<DerivedNoticeEvent>();
        harness.Bus.Subscribe(typeof(DerivedNoticeEvent), handler);

        await harness.Bus.PublishAsync(new PlainNoticeEvent());

        Assert.Empty(handler.Received);
    }

    /// <summary>
    /// 泛型参数可继承的事件会额外触发基类版本的事件
    /// </summary>
    /// <remarks>
    /// 例如触发「学生已创建」时，订阅了「人员已创建」的处理器也应收到通知，
    /// 基类版本的事件用原事件提供的构造参数重建，实体引用保持不变。
    /// </remarks>
    [Fact]
    public async Task PublishAsync_WithInheritableGenericArgument_AlsoPublishesBaseVersion()
    {
        using var harness = LocalEventBusHarness.Create();
        var derivedHandler = new RecordingLocalHandler<InheritableNoticeEvent<DerivedNoticeEvent>>();
        var baseHandler = new RecordingLocalHandler<InheritableNoticeEvent<PlainNoticeEvent>>();
        harness.Bus.Subscribe(typeof(InheritableNoticeEvent<DerivedNoticeEvent>), derivedHandler);
        harness.Bus.Subscribe(typeof(InheritableNoticeEvent<PlainNoticeEvent>), baseHandler);
        var entity = new DerivedNoticeEvent { Message = "级联" };

        await harness.Bus.PublishAsync(new InheritableNoticeEvent<DerivedNoticeEvent>(entity));

        Assert.Single(derivedHandler.Received);
        Assert.Same(entity, Assert.Single(baseHandler.Received).Entity);
    }

    /// <summary>
    /// 发布本地事件消息时按消息声明的事件类型路由，而不是按数据的运行时类型
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithLocalEventMessage_RoutesByDeclaredEventType()
    {
        using var harness = LocalEventBusHarness.Create();
        var baseHandler = new RecordingLocalHandler<PlainNoticeEvent>();
        var derivedHandler = new RecordingLocalHandler<DerivedNoticeEvent>();
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), baseHandler);
        harness.Bus.Subscribe(typeof(DerivedNoticeEvent), derivedHandler);
        var message = new LocalEventMessage(Guid.NewGuid(), new DerivedNoticeEvent(), typeof(PlainNoticeEvent));

        await harness.Bus.PublishAsync(message);

        Assert.Single(baseHandler.Received);
        Assert.Empty(derivedHandler.Received);
    }

    /// <summary>
    /// 本地事件消息原样保留构造时传入的三要素
    /// </summary>
    [Fact]
    public void LocalEventMessage_RetainsConstructorArguments()
    {
        var messageId = Guid.NewGuid();
        var eventData = new PlainNoticeEvent();

        var message = new LocalEventMessage(messageId, eventData, typeof(PlainNoticeEvent));

        Assert.Equal(messageId, message.MessageId);
        Assert.Same(eventData, message.EventData);
        Assert.Equal(typeof(PlainNoticeEvent), message.EventType);
    }

    /// <summary>
    /// 存在环境工作单元时事件被登记到工作单元而不是立刻触发
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenUnitOfWorkActive_DefersEventToUnitOfWork()
    {
        using var harness = LocalEventBusHarness.Create();
        var handler = new RecordingLocalHandler<PlainNoticeEvent>();
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), handler);
        var unitOfWork = harness.StartUnitOfWork();
        var eventData = new PlainNoticeEvent();

        await harness.Bus.PublishAsync(eventData);

        Assert.Empty(handler.Received);
        var record = Assert.Single(unitOfWork.LocalEvents);
        Assert.Equal(typeof(PlainNoticeEvent), record.EventType);
        Assert.Same(eventData, record.EventData);
    }

    /// <summary>
    /// 显式要求立即发布时即使存在工作单元也直接触发处理器
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenImmediateRequested_IgnoresAmbientUnitOfWork()
    {
        using var harness = LocalEventBusHarness.Create();
        var handler = new RecordingLocalHandler<PlainNoticeEvent>();
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), handler);
        var unitOfWork = harness.StartUnitOfWork();

        await harness.Bus.PublishAsync(new PlainNoticeEvent(), onUnitOfWorkComplete: false);

        Assert.Single(handler.Received);
        Assert.Empty(unitOfWork.LocalEvents);
    }

    /// <summary>
    /// 事件类型为空时发布失败
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenEventTypeNull_Throws()
    {
        using var harness = LocalEventBusHarness.Create();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => harness.Bus.PublishAsync((Type)null!, new PlainNoticeEvent()));
    }

    /// <summary>
    /// 事件数据为空时发布失败
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenEventDataNull_Throws()
    {
        using var harness = LocalEventBusHarness.Create();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => harness.Bus.PublishAsync(typeof(PlainNoticeEvent), null!));
    }

    /// <summary>
    /// 直接触发处理器与走发布路径的效果一致
    /// </summary>
    [Fact]
    public async Task TriggerHandlersAsync_InvokesSubscribedHandlers()
    {
        using var harness = LocalEventBusHarness.Create();
        var handler = new RecordingLocalHandler<PlainNoticeEvent>();
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), handler);

        await harness.Bus.TriggerHandlersAsync(typeof(PlainNoticeEvent), new PlainNoticeEvent());

        Assert.Single(handler.Received);
    }

    /// <summary>
    /// 没有任何订阅者时触发不抛异常
    /// </summary>
    [Fact]
    public async Task TriggerHandlersAsync_WithoutSubscribers_Completes()
    {
        using var harness = LocalEventBusHarness.Create();

        await harness.Bus.TriggerHandlersAsync(typeof(PlainNoticeEvent), new PlainNoticeEvent());
    }

    /// <summary>
    /// 触发时事件数据为空则抛出参数异常
    /// </summary>
    [Fact]
    public async Task TriggerHandlersAsync_WhenEventDataNull_Throws()
    {
        using var harness = LocalEventBusHarness.Create();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => harness.Bus.TriggerHandlersAsync(typeof(PlainNoticeEvent), null!));
    }

    /// <summary>
    /// 多租户事件在处理期间切换到事件自身的租户
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithMultiTenantEvent_SwitchesToEventTenant()
    {
        using var harness = LocalEventBusHarness.Create();
        var handler = new TenantCapturingHandler<MultiTenantNoticeEvent>(harness.CurrentTenant);
        harness.Bus.Subscribe(typeof(MultiTenantNoticeEvent), handler);

        await harness.Bus.PublishAsync(new MultiTenantNoticeEvent { TenantId = 99 });

        var captured = Assert.Single(handler.CapturedTenantIds);
        Assert.True(captured.HasValue);
        Assert.Equal(99L, captured.Value);
    }

    /// <summary>
    /// 未实现多租户接口但带 TenantId 属性的事件同样切换租户
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithPlainTenantIdProperty_SwitchesToThatTenant()
    {
        using var harness = LocalEventBusHarness.Create();
        var handler = new TenantCapturingHandler<PlainTenantEvent>(harness.CurrentTenant);
        harness.Bus.Subscribe(typeof(PlainTenantEvent), handler);

        await harness.Bus.PublishAsync(new PlainTenantEvent { TenantId = 42 });

        var captured = Assert.Single(handler.CapturedTenantIds);
        Assert.True(captured.HasValue);
        Assert.Equal(42L, captured.Value);
    }

    /// <summary>
    /// 可选多租户事件声明了租户时按声明值切换
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenEventDeclaresTenant_SwitchesToDeclaredTenant()
    {
        using var harness = LocalEventBusHarness.Create();
        var handler = new TenantCapturingHandler<MaybeTenantNoticeEvent>(harness.CurrentTenant);
        harness.Bus.Subscribe(typeof(MaybeTenantNoticeEvent), handler);

        await harness.Bus.PublishAsync(new MaybeTenantNoticeEvent { HasTenant = true, DeclaredTenantId = 7 });

        var captured = Assert.Single(handler.CapturedTenantIds);
        Assert.True(captured.HasValue);
        Assert.Equal(7L, captured.Value);
    }

    /// <summary>
    /// 事件没有租户信息时沿用环境租户
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenEventHasNoTenantInfo_KeepsAmbientTenant()
    {
        using var harness = LocalEventBusHarness.Create();
        var handler = new TenantCapturingHandler<MaybeTenantNoticeEvent>(harness.CurrentTenant);
        harness.Bus.Subscribe(typeof(MaybeTenantNoticeEvent), handler);
        using var ambient = harness.CurrentTenant.Change(5);

        await harness.Bus.PublishAsync(new MaybeTenantNoticeEvent { HasTenant = false, DeclaredTenantId = 7 });

        var captured = Assert.Single(handler.CapturedTenantIds);
        Assert.True(captured.HasValue);
        Assert.Equal(5L, captured.Value);
    }

    /// <summary>
    /// 处理结束后还原调用前的租户上下文
    /// </summary>
    [Fact]
    public async Task PublishAsync_AfterHandling_RestoresAmbientTenant()
    {
        using var harness = LocalEventBusHarness.Create();
        var handler = new TenantCapturingHandler<MultiTenantNoticeEvent>(harness.CurrentTenant);
        harness.Bus.Subscribe(typeof(MultiTenantNoticeEvent), handler);

        await harness.Bus.PublishAsync(new MultiTenantNoticeEvent { TenantId = 99 });

        Assert.Null(harness.CurrentTenant.Id);
    }
}
