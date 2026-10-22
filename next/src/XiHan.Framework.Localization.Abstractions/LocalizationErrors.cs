// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Abstractions;

/// <summary>
/// 本地化错误码；登记在 <c>spec/conventions/error-codes.md</c>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class LocalizationErrors
{
    /// <summary>区域名不合法。</summary>
    public const string InvalidLocale = "LOCALIZATION_VALIDATION_001";

    /// <summary>消息目录不合法（JSON 结构、键或区域名错误）。</summary>
    public const string InvalidCatalog = "LOCALIZATION_VALIDATION_002";

    /// <summary>同名消息目录重复注册。</summary>
    public const string DuplicateCatalog = "LOCALIZATION_CONFLICT_001";
}
