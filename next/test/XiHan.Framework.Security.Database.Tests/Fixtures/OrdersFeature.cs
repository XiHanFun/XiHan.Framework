// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.Database.Tests.Fixtures;

/// <summary>
/// 模拟业务能力包：登记权限 <c>orders.write</c> 与要求它的 <see cref="WriteOrderOperation"/>。
/// </summary>
internal sealed class OrdersFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = TestManifests.Create("xihan.app.orders", requires: ["security.authorization@1"]);

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        context.AddPermission(new PermissionDefinition(WriteOrderOperation.Permission, "写订单"));
        context.AddOperation<WriteOrderOperation, OrderRequest, OrderResponse>(WriteOrderOperation.Contract, OrdersJsonContext.Default.OrderRequest, OrdersJsonContext.Default.OrderResponse);
    }
}
