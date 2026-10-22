// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Protocols.CloudEvents.Tests;

/// <summary>
/// 订阅 <c>orders</c> 主题的测试特性。
/// </summary>
internal sealed class EventsFeature : IFeature
{
    /// <summary>
    /// 订阅的主题。
    /// </summary>
    public const string Topic = "orders";

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.test.events",
        Package = "XiHan.Framework.Protocols.CloudEvents.Tests",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version), new CapabilityRequirement(MessagingCapabilities.Queue, MessagingCapabilities.Version)],
        Entry = typeof(EventsFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context) => context.Services.AddMessageHandler<RecordingHandler>(Topic);
}
