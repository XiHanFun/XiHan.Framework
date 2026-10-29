// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Hosting.CommandLine.Tests;

/// <summary>
/// 登记测试操作的特性。
/// </summary>
/// <remarks>
/// <c>orders.create</c>（命令行 + HTTP，需 <c>orders.write</c>）、<c>orders.get</c>（命令行）、<c>orders.secret</c>（只给 HTTP）、<c>orders.audit</c>（内部）。
/// </remarks>
internal sealed class EchoFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.test.echo",
        Package = "XiHan.Framework.Hosting.CommandLine.Tests",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version), new CapabilityRequirement("security.authorization", 1)],
        Entry = typeof(EchoFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        context.AddPermission(new PermissionDefinition("orders.read", "Read orders"));
        context.AddPermission(new PermissionDefinition("orders.write", "Place orders"));
        Add(context, new OperationContract("orders.create", expose: Expose.CommandLine | Expose.Http, permission: "orders.write"));
        Add(context, new OperationContract("orders.get", expose: Expose.CommandLine));
        Add(context, new OperationContract("orders.secret", expose: Expose.Http));
        Add(context, new OperationContract("orders.audit", Stability.Internal, Expose.All));
    }

    /// <summary>
    /// 以同一实现登记一个契约。
    /// </summary>
    private static void Add(FeatureContext context, OperationContract contract)
        => context.AddOperation<EchoOperation, EchoRequest, EchoReply>(contract, EchoJsonContext.Default.EchoRequest, EchoJsonContext.Default.EchoReply);
}
