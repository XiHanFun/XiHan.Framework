// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions;

namespace XiHan.Framework.Plugins.Protocol;

/// <summary>
/// 一次成功握手的结果。
/// </summary>
/// <param name="ProtocolVersion">协商出的协议版本。</param>
/// <param name="CanonicalManifest">插件清单的规范化 JSON，用于重启后核对。</param>
/// <param name="Operations">插件服务的操作。</param>
/// <param name="Subscriptions">插件订阅的主题。</param>
/// <param name="Catalogs">插件的消息目录。</param>
/// <param name="Sdk">插件 SDK 描述（名 / 版本 / 语言）。</param>
internal sealed record HandshakeInfo(
    uint ProtocolVersion,
    string CanonicalManifest,
    IReadOnlyList<ProxyOperation> Operations,
    IReadOnlyList<PluginSubscription> Subscriptions,
    IReadOnlyList<MessageCatalog> Catalogs,
    string Sdk)
{
    /// <summary>
    /// 两次握手是否描述同一个插件：清单、操作 ID 与订阅都相同。
    /// </summary>
    public bool IsSameAs(HandshakeInfo other)
        => string.Equals(CanonicalManifest, other.CanonicalManifest, StringComparison.Ordinal)
            && Operations.Select(static o => o.Contract.Id).SequenceEqual(other.Operations.Select(static o => o.Contract.Id), StringComparer.Ordinal)
            && Subscriptions.SequenceEqual(other.Subscriptions);
}
