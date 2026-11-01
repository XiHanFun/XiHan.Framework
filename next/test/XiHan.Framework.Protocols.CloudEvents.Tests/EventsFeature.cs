// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Protocols.CloudEvents.Tests;

/// <summary>
/// 订阅测试主题的测试特性。
/// </summary>
/// <remarks>
/// <c>orders</c> 竞争订阅；<c>notices</c> 竞争与广播各一个订阅；<c>caches</c> 只有广播订阅。
/// </remarks>
internal sealed class EventsFeature : IFeature
{
    /// <summary>
    /// 订阅的主题。
    /// </summary>
    public const string Topic = "orders";

    /// <summary>
    /// 两种方式都订阅的主题。
    /// </summary>
    public const string NoticesTopic = "notices";

    /// <summary>
    /// 只以广播方式订阅的主题。
    /// </summary>
    public const string CachesTopic = "caches";

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
    public void Configure(FeatureContext context) => context.Services
        .AddMessageHandler<RecordingHandler>(Topic)
        .AddMessageHandler<RecordingHandler>(NoticesTopic)
        .AddMessageHandler<RecordingHandler>(NoticesTopic, SubscriptionMode.Broadcast)
        .AddMessageHandler<RecordingHandler>(CachesTopic, SubscriptionMode.Broadcast);
}
