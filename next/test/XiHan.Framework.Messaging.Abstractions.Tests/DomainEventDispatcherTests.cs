// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Domain.Events;
using XiHan.Framework.Messaging.Abstractions.DomainEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Tests;

/// <summary>
/// 领域事件派发。
/// </summary>
/// <remarks>
/// 登记表的去重与容量、<c>AddDomainEvent</c> 的注册、事件到信封的映射、按主题分组批量发布、未登记与发布失败、成功后清空缓冲。
/// </remarks>
public sealed class DomainEventDispatcherTests
{
    /// <summary>
    /// 基准时刻。
    /// </summary>
    private static readonly DateTimeOffset _when = new(2026, 10, 27, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 构造容器：登记两种事件（不同主题）与捕获发布者。
    /// </summary>
    private static (ServiceProvider Provider, CapturingPublisher Publisher) Build()
    {
        var publisher = new CapturingPublisher();
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IIdGenerator<Guid>, GuidV7IdGenerator>();
        services.AddScoped<IMessagePublisher>(_ => publisher);
        services.AddDomainEvent<OrderCreatedEvent>("orders", "orders.created", DomainEventJsonContext.Default.OrderCreatedEvent, static e => $"order/{e.OrderId:N}");
        services.AddDomainEvent<OrderShippedEvent>("shipments", "shipments.created", DomainEventJsonContext.Default.OrderShippedEvent);
        return (services.BuildServiceProvider(), publisher);
    }

    [Fact]
    public void Registry_RejectsDuplicates_AndEnforcesCapacity()
    {
        var registry = new DomainEventRegistry(capacity: 1);
        var registration = new DomainEventRegistration(typeof(OrderCreatedEvent), "orders", "orders.created", DomainEventJsonContext.Default.OrderCreatedEvent, null);

        registry.Add(registration);
        Assert.Equal(1, registry.Count);
        Assert.Same(registration, registry.Find(typeof(OrderCreatedEvent)).ValueOrThrow());
        Assert.False(registry.Find(typeof(OrderShippedEvent)).HasValue);
        Assert.Equal(MessagingErrors.DomainEventRegistration, Assert.Throws<FrameworkException>(() => registry.Add(registration)).Code);
        Assert.Equal(MessagingErrors.DomainEventRegistration, Assert.Throws<FrameworkException>(() => registry.Add(new DomainEventRegistration(typeof(OrderShippedEvent), "shipments", "shipments.created", DomainEventJsonContext.Default.OrderShippedEvent, null))).Code);
        Assert.Throws<ArgumentException>(static () => new DomainEventRegistration(typeof(OrderCreatedEvent), "Bad Topic", "orders.created", DomainEventJsonContext.Default.OrderCreatedEvent, null));
        Assert.Throws<ArgumentException>(static () => new DomainEventRegistration(typeof(OrderCreatedEvent), "orders", " ", DomainEventJsonContext.Default.OrderCreatedEvent, null));
        Assert.Throws<ArgumentOutOfRangeException>(static () => new DomainEventRegistry(0));
    }

    [Fact]
    public void AddDomainEvent_SharesOneRegistry_AndRegistersTheDispatcher()
    {
        var services = new ServiceCollection();
        services.AddDomainEvent<OrderCreatedEvent>("orders", "orders.created", DomainEventJsonContext.Default.OrderCreatedEvent);
        services.AddDomainEvent<OrderShippedEvent>("shipments", "shipments.created", DomainEventJsonContext.Default.OrderShippedEvent);

        var registry = services.GetDomainEventRegistry();
        Assert.Equal(2, registry.Count);
        Assert.Single(services, static d => d.ServiceType == typeof(DomainEventRegistry));
        Assert.Single(services, static d => d.ServiceType == typeof(IDomainEventDispatcher) && d.ImplementationType == typeof(DomainEventDispatcher) && d.Lifetime == ServiceLifetime.Scoped);
        Assert.Equal(MessagingErrors.DomainEventRegistration, Assert.Throws<FrameworkException>(() => services.AddDomainEvent<OrderCreatedEvent>("orders", "orders.created", DomainEventJsonContext.Default.OrderCreatedEvent)).Code);
    }

    [Fact]
    public async Task Dispatch_MapsEventsToEnvelopes_GroupsByTopic_AndClears()
    {
        var (provider, publisher) = Build();
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
        var orderId = Guid.CreateVersion7();
        var first = new TestAggregate();
        first.Raise(new OrderCreatedEvent(orderId, _when));
        first.Raise(new OrderShippedEvent(orderId, _when.AddHours(1)));
        var second = new TestAggregate();
        second.Raise(new OrderCreatedEvent(Guid.CreateVersion7(), _when.AddHours(2)));

        var result = await dispatcher.DispatchAsync([first, second], "acme", CancellationToken.None);

        Assert.True(result.IsSuccess, result.ToString());
        // 主题按首次出现顺序，同一主题的事件在同一批里
        Assert.Equal(["orders", "shipments"], publisher.Batches.Select(static b => b.Topic));
        var orders = publisher.Batches[0].Envelopes;
        Assert.Equal(2, orders.Count);
        Assert.Equal("orders.created", orders[0].Type);
        Assert.Equal("/orders", orders[0].Source);
        Assert.Equal($"order/{orderId:N}", orders[0].Subject);
        Assert.Equal(_when, orders[0].Time);
        Assert.Equal("acme", orders[0].Extension(EnvelopeExtensions.Tenant).ValueOrThrow());
        Assert.Equal(32, orders[0].Id.Length);
        Assert.NotEqual(orders[0].Id, orders[1].Id);
        using var data = JsonDocument.Parse(orders[0].Data);
        Assert.Equal(orderId, data.RootElement.GetProperty("orderId").GetGuid());
        Assert.Null(publisher.Batches[1].Envelopes[0].Subject);
        Assert.Empty(first.DomainEvents);
        Assert.Empty(second.DomainEvents);
    }

    [Fact]
    public async Task Dispatch_UnregisteredEvent_FailsWithoutPublishing_AndPublishFailureKeepsBuffers()
    {
        var (provider, publisher) = Build();
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();

        var unknown = new TestAggregate();
        unknown.Raise(new OrderCreatedEvent(Guid.CreateVersion7(), _when));
        unknown.Raise(new UnregisteredEvent(_when));
        var rejected = await dispatcher.DispatchAsync([unknown], null, CancellationToken.None);
        Assert.Equal(MessagingErrors.DomainEventUnregistered, rejected.Error?.Code);
        Assert.Equal(typeof(UnregisteredEvent).FullName, rejected.Error!.Details!["event"]);
        Assert.Empty(publisher.Batches);
        Assert.Equal(2, unknown.DomainEvents.Count);

        publisher.Failure = new Error("MESSAGING_UNAVAILABLE_001", "messaging.broker.unavailable");
        var aggregate = new TestAggregate();
        aggregate.Raise(new OrderCreatedEvent(Guid.CreateVersion7(), _when));
        var failed = await dispatcher.DispatchAsync([aggregate], null, CancellationToken.None);
        Assert.Equal("MESSAGING_UNAVAILABLE_001", failed.Error?.Code);
        Assert.Single(aggregate.DomainEvents);
        Assert.Single(publisher.Batches);
        Assert.False(publisher.Batches[0].Envelopes[0].Extension(EnvelopeExtensions.Tenant).HasValue);
    }

    /// <summary>
    /// 没有登记的事件。
    /// </summary>
    private sealed record UnregisteredEvent(DateTimeOffset OccurredAt) : IDomainEvent;
}
