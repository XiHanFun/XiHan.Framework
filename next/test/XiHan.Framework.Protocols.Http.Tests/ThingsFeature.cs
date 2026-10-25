// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Protocols.Http.Tests;

/// <summary>
/// 登记两个请求都不含 <c>id</c> 的操作 <c>things.list</c> 与 <c>things.get</c>：按约定都推导为 <c>GET /things</c>，映射端点时必须报路由冲突。
/// </summary>
internal sealed class ThingsFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.test.things",
        Package = "XiHan.Framework.Protocols.Http.Tests",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(ThingsFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        foreach (var id in new[] { "things.list", "things.get" })
        {
            context.AddOperation<OrderReportOperation, OrderReportRequest, OrderReply>(new OperationContract(id, expose: Expose.Http), OrdersJsonContext.Default.OrderReportRequest, OrdersJsonContext.Default.OrderReply);
        }
    }
}
