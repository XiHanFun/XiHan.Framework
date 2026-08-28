// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.EventBus.Abstractions.Tests.Distributed;

/// <summary>
/// 发件箱契约测试
/// </summary>
/// <remarks>
/// 抽象包只定义发件箱的操作集合，这里用一个最小内存实现把「入队 → 取待发 → 回灌事件总线 → 删除」
/// 这条发件箱主流程跑通，验证接口签名确实支撑得起这条流程：
/// 过滤条件声明在只读接口 <see cref="IOutgoingEventInfo"/> 上，实现类 <see cref="OutgoingEventInfo"/> 必须能被它筛；
/// 删除按事件唯一标识而不是按对象引用，才能在跨进程重放后仍然定位得到。
/// </remarks>
public class EventOutboxContractTests
{
    /// <summary>
    /// 入队后可从待发列表取回
    /// </summary>
    [Fact]
    public async Task GetWaitingEvents_AfterEnqueue_ReturnsEnqueuedEvent()
    {
        var outbox = new InMemoryEventOutbox();
        var outgoing = EventInfoFactory.CreateOutgoing();

        await outbox.EnqueueAsync(outgoing);
        var waiting = await outbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(outgoing, Assert.Single(waiting));
    }

    /// <summary>
    /// 待发数量受最大条数约束，保证批处理不会一次拉爆
    /// </summary>
    [Fact]
    public async Task GetWaitingEvents_RespectsMaxCount()
    {
        var outbox = new InMemoryEventOutbox();
        for (var index = 0; index < 5; index++)
        {
            await outbox.EnqueueAsync(EventInfoFactory.CreateOutgoing($"sample.event.{index}"));
        }

        var waiting = await outbox.GetWaitingEventsAsync(2, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, waiting.Count);
    }

    /// <summary>
    /// 过滤条件按只读接口书写即可作用在实现类上
    /// </summary>
    [Fact]
    public async Task GetWaitingEvents_WithFilter_FiltersByReadOnlyContract()
    {
        var outbox = new InMemoryEventOutbox();
        await outbox.EnqueueAsync(EventInfoFactory.CreateOutgoing("sample.event"));
        await outbox.EnqueueAsync(EventInfoFactory.CreateOutgoing("audit.event"));

        Expression<Func<IOutgoingEventInfo, bool>> filter = x => x.EventName == "audit.event";
        var waiting = await outbox.GetWaitingEventsAsync(10, filter, TestContext.Current.CancellationToken);

        Assert.Equal("audit.event", Assert.Single(waiting).EventName);
    }

    /// <summary>
    /// 发件箱主流程：取待发 → 交给事件总线回灌 → 按唯一标识批量删除
    /// </summary>
    [Fact]
    public async Task OutboxFlow_PublishThenDeleteMany_DrainsOutbox()
    {
        var outbox = new InMemoryEventOutbox();
        var bus = new RecordingEventBoxesBus();
        var outboxConfig = new OutboxConfig("Default");

        await outbox.EnqueueAsync(EventInfoFactory.CreateOutgoing("sample.event"));
        await outbox.EnqueueAsync(EventInfoFactory.CreateOutgoing("audit.event"));

        var waiting = await outbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);
        await bus.PublishManyFromOutboxAsync(waiting, outboxConfig);
        await outbox.DeleteManyAsync(waiting.Select(x => x.Id));

        Assert.Equal(2, bus.Published.Count);
        Assert.Equal("Default", Assert.Single(bus.OutboxNames));
        Assert.Empty(outbox.Events);
    }

    /// <summary>
    /// 单条回灌路径同样按配置名区分发件箱
    /// </summary>
    [Fact]
    public async Task PublishFromOutbox_CarriesOutboxName()
    {
        var bus = new RecordingEventBoxesBus();
        var outgoing = EventInfoFactory.CreateOutgoing();

        await bus.PublishFromOutboxAsync(outgoing, new OutboxConfig("Audit"));

        Assert.Same(outgoing, Assert.Single(bus.Published));
        Assert.Equal("Audit", Assert.Single(bus.OutboxNames));
    }

    /// <summary>
    /// 删除按唯一标识定位，不依赖对象引用
    /// </summary>
    [Fact]
    public async Task Delete_ById_RemovesMatchingEventOnly()
    {
        var outbox = new InMemoryEventOutbox();
        var target = EventInfoFactory.CreateOutgoing("sample.event");
        var survivor = EventInfoFactory.CreateOutgoing("audit.event");

        await outbox.EnqueueAsync(target);
        await outbox.EnqueueAsync(survivor);
        await outbox.DeleteAsync(target.Id);

        Assert.Same(survivor, Assert.Single(outbox.Events));
    }

    /// <summary>
    /// 发件箱接口全异步，且过滤条件与取消令牌都是可选参数
    /// </summary>
    [Fact]
    public void EventOutbox_GetWaitingEvents_HasOptionalFilterAndToken()
    {
        var method = typeof(IEventOutbox).GetMethod(
            nameof(IEventOutbox.GetWaitingEventsAsync),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.NotNull(method);

        var parameters = method.GetParameters();

        Assert.Equal(3, parameters.Length);
        Assert.Equal(typeof(int), parameters[0].ParameterType);
        Assert.Equal(typeof(Expression<Func<IOutgoingEventInfo, bool>>), parameters[1].ParameterType);
        Assert.True(parameters[1].HasDefaultValue);
        Assert.Equal(typeof(CancellationToken), parameters[2].ParameterType);
        Assert.True(parameters[2].HasDefaultValue);
        Assert.Equal(typeof(Task<List<OutgoingEventInfo>>), method.ReturnType);
    }
}
