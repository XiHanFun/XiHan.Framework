// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Protocols.Http.Tests;

/// <summary>
/// 登记七个操作 ID 的测试特性：五个标准动词与一个自定义动词暴露给 HTTP，一个只给 MCP，一个标为内部。
/// </summary>
internal sealed class OrdersFeature : IFeature
{
    /// <summary>
    /// 暴露给 HTTP 且非内部的操作 ID。
    /// </summary>
    public static readonly string[] HttpOperations = ["orders.cancel", "orders.create", "orders.delete", "orders.export", "orders.get", "orders.list", "orders.update"];

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.test.orders",
        Package = "XiHan.Framework.Protocols.Http.Tests",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(OrdersFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        foreach (var id in HttpOperations)
        {
            Add(context, new OperationContract(id, expose: Expose.Http));
        }

        Add(context, new OperationContract("orders.secret", expose: Expose.Mcp));
        Add(context, new OperationContract("orders.audit", Stability.Internal, Expose.All));
    }

    /// <summary>
    /// 以同一实现登记一个契约。
    /// </summary>
    private static void Add(FeatureContext context, OperationContract contract)
        => context.AddOperation<OrdersOperation, OrderRequest, OrderReply>(contract, OrdersJsonContext.Default.OrderRequest, OrdersJsonContext.Default.OrderReply);
}
