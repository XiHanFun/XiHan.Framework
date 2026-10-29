// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Auditing.Abstractions.Tests.Fixtures;

/// <summary>
/// 登记三个操作的测试特性。
/// </summary>
/// <remarks>
/// 声明了权限的创建、未声明权限的列表、显式声明审计动作的导出。
/// </remarks>
internal sealed class OrdersFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = TestManifests.Create("xihan.test.orders", requires: ["kernel.hosting@1"], kind: ManifestKind.Capability);

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        context.AddOperation<CreateOrder, CreateOrderRequest, OrderResponse>(new(CreateOrder.Id, permission: "orders.write"), OrdersJsonContext.Default.CreateOrderRequest, OrdersJsonContext.Default.OrderResponse);
        context.AddOperation<ListOrders, ListOrdersRequest, OrderResponse>(new(ListOrders.Id), OrdersJsonContext.Default.ListOrdersRequest, OrdersJsonContext.Default.OrderResponse);
        context.AddOperation<ExportOrders, ListOrdersRequest, OrderResponse>(new(ExportOrders.Id), OrdersJsonContext.Default.ListOrdersRequest, OrdersJsonContext.Default.OrderResponse);
    }
}
