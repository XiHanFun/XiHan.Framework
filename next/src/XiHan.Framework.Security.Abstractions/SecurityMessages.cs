// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions;

/// <summary>
/// 安全子系统的消息键；各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>，Provider 装配时注册 <see cref="Catalog"/>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class SecurityMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "security";

    /// <summary>调用需要认证。</summary>
    public const string AuthenticationRequired = "security.authentication.required";

    /// <summary>认证方案不受支持。参数：<c>scheme</c>。</summary>
    public const string UnsupportedScheme = "security.authentication.unsupported_scheme";

    /// <summary>策略不存在。参数：<c>policy</c>。</summary>
    public const string PolicyNotFound = "security.policy.not_found";

    /// <summary>缺少权限。参数：<c>permission</c>。</summary>
    public const string PermissionMissing = "security.permission.missing";

    /// <summary>要撤销的权限授予不存在。参数：<c>subject</c>、<c>permission</c>、<c>tenant</c>（全局为 <c>*</c>）。</summary>
    public const string GrantNotFound = "security.grant.not_found";

    /// <summary>API Key 无效。</summary>
    public const string InvalidApiKey = "security.api_key.invalid";

    /// <summary>授予存储容量已满。参数：<c>capacity</c>。</summary>
    public const string GrantStoreCapacityExceeded = "security.grant_store.capacity_exceeded";

    /// <summary>主体存储容量已满。参数：<c>capacity</c>。</summary>
    public const string PrincipalStoreCapacityExceeded = "security.principal_store.capacity_exceeded";

    /// <summary>令牌无效、过期或签名不符。</summary>
    public const string InvalidToken = "security.token.invalid";

    /// <summary>令牌缺少主体声明。参数：<c>claim</c>。</summary>
    public const string TokenSubjectMissing = "security.token.subject_missing";

    /// <summary>签发者的元数据不可达。参数：<c>authority</c>。</summary>
    public const string IssuerUnavailable = "security.issuer.unavailable";

    /// <summary>
    /// 延迟装载的目录；首次访问时读嵌入的语言文件，之后复用同一实例。
    /// </summary>
    private static readonly Lazy<MessageCatalog> _catalog = new(static () => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(SecurityMessages)));

    /// <summary>
    /// 目录实例；首次访问时从嵌入资源装载。
    /// </summary>
    public static MessageCatalog Catalog => _catalog.Value;
}
