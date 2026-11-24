// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Protocols.Grpc.Tests.Fixtures;

/// <summary>
/// 登记一个已弃用、暴露给 gRPC 的操作
/// </summary>
/// <remarks>
/// <c>orders.archive</c> 的稳定性是 <see cref="Stability.Deprecated"/>，请求与响应沿用订单的 Schema：验证 <c>.proto</c> 里只有它的 <c>rpc</c> 带 <c>option deprecated = true;</c>，调用照常成功（ADR-0280）
/// </remarks>
internal sealed class DeprecatedFeature : IFeature
{
    /// <summary>
    /// 弃用的操作的 ID
    /// </summary>
    public const string OperationId = "orders.archive";

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.test.deprecated",
        Package = "XiHan.Framework.Protocols.Grpc.Tests",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(DeprecatedFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
        => context.AddOperation<OrdersOperation, OrderRequest, OrderReply>(new OperationContract(OperationId) { Stability = Stability.Deprecated, Expose = Expose.Grpc }, OrdersJsonContext.Default.OrderRequest, OrdersJsonContext.Default.OrderReply, OrdersFeature.OrderSchema);
}
