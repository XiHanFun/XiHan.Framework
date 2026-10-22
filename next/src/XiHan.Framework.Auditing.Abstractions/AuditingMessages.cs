// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions;

/// <summary>
/// 审计子系统的消息键；各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>，Provider 装配时注册 <see cref="Catalog"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class AuditingMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "auditing";

    /// <summary>查询不合法。参数：<c>reason</c>。</summary>
    public const string QueryInvalid = "auditing.query.invalid";

    /// <summary>审计存储不可达。参数：<c>reason</c>。</summary>
    public const string TrailUnavailable = "auditing.trail.unavailable";

    /// <summary>
    /// 本包的消息目录。
    /// </summary>
    public static MessageCatalog Catalog => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(AuditingMessages));
}
