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

namespace SimpleApp;

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
    /// 登记订单仓储、事件计数器、<see cref="Topic"/> 的消息处理器，以及清单里声明的两个权限。
    /// </summary>
    public void Configure(FeatureContext context)
    {
        context.Services.AddRepository<Order, Guid>();
        context.Services.AddSingleton<OrderEventCounter>();
        context.Services.AddMessageHandler<OrderPlacedHandler>(Topic);

        // 权限码必须先在这里登记，端点上的 RequireXiHanPermission 才能引用；未登记的权限码在装配时报错
        context.AddPermission(new PermissionDefinition("orders.read", "Read orders"));
        context.AddPermission(new PermissionDefinition("orders.write", "Place orders"));
    }
}
