// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Auditing.Database.Tests.Fixtures;

/// <summary>
/// 登记一个声明了权限的创建操作的测试特性：默认选择规则下它的调用会被审计。
/// </summary>
internal sealed class OrdersFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = TestManifests.Create("xihan.test.orders", requires: ["kernel.hosting@1"], kind: ManifestKind.Capability);

    /// <inheritdoc />
    public void Configure(FeatureContext context)
        => context.AddOperation<CreateOrder, CreateOrderRequest, OrderResponse>(new(CreateOrder.Id, permission: "orders.write"), OrdersJsonContext.Default.CreateOrderRequest, OrdersJsonContext.Default.OrderResponse);
}
