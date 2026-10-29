// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Protocols.Http.Tests.Fixtures;

/// <summary>
/// 照 <c>samples/SimpleApp</c> 的 <c>OrdersFeature</c> 装配的特性。
/// </summary>
/// <remarks>
/// 登记 <c>orders.read</c> / <c>orders.write</c> 两个权限定义，以及按同样契约（Experimental、暴露给 HTTP / gRPC / MCP、按权限码授权）登记的 <c>orders.create</c> 与 <c>orders.list</c>。示例不是库，测试不引用它。
/// </remarks>
internal sealed class SampleOrdersFeature : IFeature
{
    /// <summary>
    /// 读订单权限的显示名，与示例一致。
    /// </summary>
    public const string ReadDisplayName = "Read orders";

    /// <summary>
    /// 下单权限的显示名，与示例一致。
    /// </summary>
    public const string WriteDisplayName = "Place orders";

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.test.sample-orders",
        Package = "XiHan.Framework.Protocols.Http.Tests",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions { Permissions = ["orders.read", "orders.write"] },
        Entry = typeof(SampleOrdersFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        // 与示例相同：权限先登记，操作契约再引用
        context.AddPermission(new PermissionDefinition("orders.read", ReadDisplayName));
        context.AddPermission(new PermissionDefinition("orders.write", WriteDisplayName));

        const Expose expose = Expose.Http | Expose.Grpc | Expose.Mcp;
        context.AddOperation<OrdersOperation, OrderRequest, OrderReply>(new OperationContract("orders.create", Stability.Experimental, expose, permission: "orders.write"), OrdersJsonContext.Default.OrderRequest, OrdersJsonContext.Default.OrderReply);
        context.AddOperation<OrdersOperation, OrderRequest, OrderReply>(new OperationContract("orders.list", Stability.Experimental, expose, permission: "orders.read"), OrdersJsonContext.Default.OrderRequest, OrdersJsonContext.Default.OrderReply);
    }
}
