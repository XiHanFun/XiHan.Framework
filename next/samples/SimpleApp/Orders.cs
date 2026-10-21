// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Security.Abstractions;

namespace SimpleApp;

/// <summary>
/// 订单：多租户、带创建时间、软删除的聚合根。持久化子系统按协议自动维护这些字段。
/// </summary>
public sealed class Order : IAggregateRoot<Guid>, IMultiTenant, IHasCreationTime, ISoftDeletable
{
    private readonly DomainEventBuffer _events = new();

    public Guid Id { get; init; }

    public string TenantId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Amount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _events.Items;

    public void Place(DateTimeOffset now) => _events.Add(new OrderPlaced(Id, now));

    public void ClearDomainEvents() => _events.Clear();
}

public sealed record OrderPlaced(Guid OrderId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PlaceOrderRequest(string Name, int Amount);

public sealed record OrderResponse(Guid Id, string Name, int Amount, DateTimeOffset CreatedAt);

public sealed record OrderStatistics(long Handled);

/// <summary>
/// 已处理的订单事件计数。
/// </summary>
public sealed class OrderEventCounter
{
    private long _handled;

    public long Handled => Interlocked.Read(ref _handled);

    public void Increment() => Interlocked.Increment(ref _handled);
}

/// <summary>
/// 订单事件处理器：消息经完整管道派发，这里能看到租户等上下文。
/// </summary>
public sealed class OrderPlacedHandler(OrderEventCounter counter) : IMessageHandler
{
    public ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken)
    {
        counter.Increment();
        return ValueTask.FromResult(Result.Success());
    }
}

/// <summary>
/// 订单能力包：按能力 ID 声明依赖，登记实体、权限与消息订阅。换 Provider 不改这里。
/// </summary>
public sealed class OrdersFeature : IFeature
{
    public const string Topic = "orders";

    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.samples.orders",
        Package = "SimpleApp",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Capability,
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(PersistenceCapabilities.Relational, PersistenceCapabilities.Version),
            new CapabilityRequirement(MessagingCapabilities.Queue, MessagingCapabilities.Version),
            new CapabilityRequirement(SecurityCapabilities.Authorization, SecurityCapabilities.Version),
            new CapabilityRequirement(MultiTenancyCapabilities.Context, MultiTenancyCapabilities.Version),
        ],
        Contributes = new Contributions { Permissions = ["orders.read", "orders.write"] },
        Entry = typeof(OrdersFeature).FullName,
    };

    public void Configure(FeatureContext context)
    {
        context.Services.AddRepository<Order, Guid>();
        context.Services.AddSingleton<OrderEventCounter>();
        context.Services.AddMessageHandler<OrderPlacedHandler>(Topic);
        context.AddPermission(new PermissionDefinition("orders.read", "读订单"));
        context.AddPermission(new PermissionDefinition("orders.write", "下订单"));
    }
}
