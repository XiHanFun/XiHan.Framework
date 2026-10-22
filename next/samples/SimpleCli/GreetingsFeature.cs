// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace SimpleCli;

/// <summary>
/// 问候特性：登记由 <c>Kernel.Generators</c> 从 <c>[Operation]</c> 生成的操作。
/// </summary>
public sealed class GreetingsFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.samples.greetings",
        Package = "SimpleCli",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(GreetingsFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context) => context.AddGeneratedOperations();
}
