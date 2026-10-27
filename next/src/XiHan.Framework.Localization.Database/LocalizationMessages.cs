// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Database;

/// <summary>
/// 数据库本地化 Provider 的消息键；各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class LocalizationMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "localization.database";

    /// <summary>
    /// 数据库里没有这条翻译。参数：<c>key</c>、<c>locale</c>。
    /// </summary>
    public const string TranslationNotFound = "localization.translation.not_found";

    /// <summary>
    /// 本包的消息目录。
    /// </summary>
    public static MessageCatalog Catalog => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(LocalizationMessages));
}
