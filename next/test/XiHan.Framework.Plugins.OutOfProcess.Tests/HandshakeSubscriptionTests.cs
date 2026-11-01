// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 握手声明的订阅方式（<c>spec/plugin/handshake.md</c> §3.5，RFC-0009「订阅方式」）。
/// </summary>
/// <remarks>
/// 直接核对握手响应：<c>mode</c> 为空或 <c>competing</c> 是竞争消费，<c>broadcast</c> 是广播，其他值与同一（主题，类型）以两种方式各声明一次都是 <c>PLUGINS_VALIDATION_004</c>；订阅只命中同一方式的投递。
/// </remarks>
public sealed class HandshakeSubscriptionTests
{
    /// <summary>
    /// 最小的进程外插件清单。
    /// </summary>
    private const string ManifestJson = """
        {
          "id": "xihan.samples.subscriber",
          "package": "Subscriber",
          "version": "5.0.0-preview.1",
          "primitives": "1",
          "stability": "experimental",
          "kind": "plugin",
          "isolation": "out-of-process",
          "requires": [ { "id": "kernel.plugins", "version": "1" } ],
          "activation": [ "onStartup" ],
          "entry": "Subscriber"
        }
        """;

    [Fact]
    public void Mode_DefaultsToCompeting_AndAcceptsBroadcast()
    {
        var info = Validate(
            new Subscription { Topic = "orders" },
            new Subscription { Topic = "orders.shipped", Mode = "competing" },
            new Subscription { Topic = "sessions.revoked", MessageType = "sessions.revoked", Mode = "broadcast" }).ValueOrThrow();

        Assert.Equal(
            [
                new PluginSubscription("orders", null, SubscriptionMode.Competing),
                new PluginSubscription("orders.shipped", null, SubscriptionMode.Competing),
                new PluginSubscription("sessions.revoked", "sessions.revoked", SubscriptionMode.Broadcast),
            ],
            info.Subscriptions);
        // 订阅只命中同一方式的投递：竞争投递不进广播订阅的插件，反之亦然
        Assert.True(info.Subscriptions[2].Matches("sessions.revoked", "sessions.revoked", SubscriptionMode.Broadcast));
        Assert.False(info.Subscriptions[2].Matches("sessions.revoked", "sessions.revoked", SubscriptionMode.Competing));
        Assert.False(info.Subscriptions[0].Matches("orders", "orders.placed", SubscriptionMode.Broadcast));
    }

    [Fact]
    public void UnknownMode_OrTheSamePairInBothModes_IsAHandshakeMismatch()
    {
        var unknown = Validate(new Subscription { Topic = "orders", Mode = "fanout" });
        var twice = Validate(new Subscription { Topic = "orders" }, new Subscription { Topic = "orders", Mode = "broadcast" });

        Assert.Equal(PluginErrors.HandshakeMismatch, unknown.Error?.Code);
        Assert.Contains("unknown mode 'fanout'", unknown.Error?.Message, StringComparison.Ordinal);
        Assert.Equal(PluginErrors.HandshakeMismatch, twice.Error?.Code);
        Assert.Contains("declared more than once", twice.Error?.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 用给定的订阅构造握手响应并核对。
    /// </summary>
    private static Result<HandshakeInfo> Validate(params Subscription[] subscriptions)
    {
        var manifest = ManifestReader.Read(ManifestJson).ValueOrThrow();
        var response = new HandshakeResponse { ProtocolVersion = PluginProtocol.Version, Primitives = 1, Manifest = ManifestJson };
        response.Subscriptions.AddRange(subscriptions);
        return HandshakeValidator.Validate(manifest, response, 1, PluginIsolation.OutOfProcess);
    }
}
