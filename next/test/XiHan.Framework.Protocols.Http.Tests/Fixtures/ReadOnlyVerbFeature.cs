// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Protocols.Http.Tests.Fixtures;

/// <summary>
/// 两个只有标量字段的自定义动词。
/// </summary>
/// <remarks>
/// <c>orders.tree</c> 声明只读，<c>orders.archive</c> 没声明——前者走 GET，后者照旧 POST。
/// </remarks>
internal sealed class ReadOnlyVerbFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.test.read-only-verbs",
        Package = "XiHan.Framework.Protocols.Http.Tests",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(ReadOnlyVerbFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        // 带 Schema：OpenAPI 才写得出查询参数
        context.AddOperation<OrdersOperation, OrderRequest, OrderReply>(new OperationContract("orders.tree", expose: Expose.Http, readOnly: true), OrdersJsonContext.Default.OrderRequest, OrdersJsonContext.Default.OrderReply, OrdersFeature.OrderSchema);
        context.AddOperation<OrdersOperation, OrderRequest, OrderReply>(new OperationContract("orders.archive", expose: Expose.Http), OrdersJsonContext.Default.OrderRequest, OrdersJsonContext.Default.OrderReply);
    }
}
