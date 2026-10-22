// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions;

/// <summary>
/// 配置子系统的消息键；各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>，Provider 装配时注册 <see cref="Catalog"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class ConfigurationMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "configuration";

    /// <summary>远程配置的命名空间不存在。参数：<c>namespace</c>。</summary>
    public const string NamespaceNotFound = "configuration.namespace.not_found";

    /// <summary>远程配置不可达。参数：<c>namespace</c>、<c>reason</c>。</summary>
    public const string RemoteUnavailable = "configuration.remote.unavailable";

    /// <summary>密钥不存在。参数：<c>name</c>。</summary>
    public const string SecretNotFound = "configuration.secret.not_found";

    /// <summary>没有读取密钥的权限。参数：<c>name</c>。</summary>
    public const string SecretDenied = "configuration.secret.denied";

    /// <summary>密钥库不可达。参数：<c>reason</c>。</summary>
    public const string SecretsUnavailable = "configuration.secrets.unavailable";

    /// <summary>
    /// 本包的消息目录。
    /// </summary>
    public static MessageCatalog Catalog => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(ConfigurationMessages));
}
