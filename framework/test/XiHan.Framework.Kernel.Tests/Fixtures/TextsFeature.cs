// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 登记 <see cref="TextsOperation"/> 的特性
/// </summary>
internal sealed class TextsFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.test.texts",
        Package = "XiHan.Framework.Kernel.Tests",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(TextsFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
        => context.AddOperation<TextsOperation, EchoRequest, TextsResponse>(new(TextsOperation.Id), EchoJsonContext.Default.EchoRequest, TextsJsonContext.Default.TextsResponse, null);
}
