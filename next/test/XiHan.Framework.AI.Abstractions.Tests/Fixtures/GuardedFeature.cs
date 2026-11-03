// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.AI.Abstractions.Tests.Fixtures;

/// <summary>
/// 登记一个声明了权限的 AI 操作 <c>guarded.whoami</c>
/// </summary>
/// <remarks>
/// 权限码 <c>guarded.read</c>；清单要求授权能力，安全特性据此装配 Authenticate 与 Authorize 阶段
/// </remarks>
internal sealed class GuardedFeature : IFeature
{
    /// <summary>
    /// 操作要求的权限码
    /// </summary>
    public const string Permission = "guarded.read";

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.test.guarded",
        Package = "XiHan.Framework.AI.Abstractions.Tests",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version), new CapabilityRequirement("security.authorization", 1)],
        Entry = typeof(GuardedFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        context.AddPermission(new PermissionDefinition(Permission, "Read guarded data"));
        context.AddOperation<WhoAmIOperation, EchoRequest, EchoResponse>(
            new OperationContract("guarded.whoami", expose: Expose.Ai, permission: Permission),
            EchoJsonContext.Default.EchoRequest,
            EchoJsonContext.Default.EchoResponse);
    }
}
