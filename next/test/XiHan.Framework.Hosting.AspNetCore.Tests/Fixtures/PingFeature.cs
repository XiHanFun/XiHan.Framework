// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Hosting.AspNetCore.Tests.Fixtures;

/// <summary>
/// 登记问候操作的测试特性。
/// </summary>
internal sealed class PingFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.test.pings",
        Package = "XiHan.Framework.Hosting.AspNetCore.Tests",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(PingFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
        => context.AddOperation<PingOperation, PingRequest, PingResponse>(new(PingOperation.Id, expose: Expose.Http), PingJsonContext.Default.PingRequest, PingJsonContext.Default.PingResponse);
}
