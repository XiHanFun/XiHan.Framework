// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Protocols.Grpc.Tests;

/// <summary>
/// 登记一个没有 Schema 却暴露给 gRPC 的操作：gRPC 适配器必须在启动时拒绝。
/// </summary>
internal sealed class SchemalessFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.test.schemaless",
        Package = "XiHan.Framework.Protocols.Grpc.Tests",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(SchemalessFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
        => context.AddOperation<OrdersOperation, OrderRequest, OrderReply>(new OperationContract("reports.create", expose: Expose.Grpc), OrdersJsonContext.Default.OrderRequest, OrdersJsonContext.Default.OrderReply);
}
