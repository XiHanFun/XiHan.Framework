// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Security.Abstractions;

namespace SimpleApp.Orders;

/// <summary>
/// 订单能力包：按能力 ID 声明依赖，登记实体、权限与消息订阅。换 Provider 不改这里。
/// </summary>
public sealed class OrdersFeature : IFeature
{
    /// <summary>
    /// 订单消息的主题；发布与订阅共用。
    /// </summary>
    public const string Topic = "orders";

    /// <summary>
    /// 清单：依赖持久化、消息、授权与多租户四个能力，并声明贡献的权限码。
    /// </summary>
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

    /// <summary>
    /// 登记订单仓储、<see cref="OrderPlaced"/> 领域事件的消息映射、事件计数器、<see cref="Topic"/> 的消息处理器、清单里声明的两个权限，以及两个操作契约。
    /// </summary>
    public void Configure(FeatureContext context)
    {
        // 仓储登记由 Persistence.Generators 从实体类生成（Order : IAggregateRoot<Guid>）
        context.Services.AddGeneratedRepositories();
        // 聚合记录的 OrderPlaced 在工作单元提交前发到 orders 主题（类型 orders.placed，subject 是订单）；装配了发件箱时与订单写入同事务
        context.Services.AddDomainEvent<OrderPlaced>(Topic, "orders.placed", SampleJsonContext.Default.OrderPlaced, static placed => $"order/{placed.OrderId:N}");
        context.Services.AddSingleton<OrderEventCounter>();
        context.Services.AddMessageHandler<OrderPlacedHandler>(Topic);

        // 权限码必须先在这里登记，操作契约与端点上的 RequireXiHanPermission 才能引用；未登记的权限码在装配时报错
        context.AddPermission(new PermissionDefinition("orders.read", "Read orders"));
        context.AddPermission(new PermissionDefinition("orders.write", "Place orders"));

        // 操作契约：类型即 Schema。登记代码由 Kernel.Generators 从 [Operation] 生成（含请求/响应 JSON Schema），
        // HTTP 适配器按操作 ID 推导路由并发布 OpenAPI，Authorize 阶段按契约的权限判定
        context.AddGeneratedOperations();
    }
}
