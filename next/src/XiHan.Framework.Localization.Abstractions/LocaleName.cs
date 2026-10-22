// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Abstractions;

/// <summary>
/// 区域名文法（BCP 47 的常用子集）：2–3 个字母的语言子标签，之后是 0 到多个 2–8 位字母数字的子标签，用 <c>-</c> 连接。
/// 比较不区分大小写；<see cref="Normalize"/> 给出规范大小写（语言小写、两位地区大写、四位文字首字母大写）。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static partial class LocaleName
{
    /// <summary>
    /// 最大长度。
    /// </summary>
    public const int MaxLength = 35;

    /// <summary>
    /// 是否合法。
    /// </summary>
    public static bool IsValid([NotNullWhen(true)] string? locale)
        => locale is not null && locale.Length <= MaxLength && Pattern().IsMatch(locale);

    /// <summary>
    /// 校验并规范化；不合法抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public static string Validate(string locale)
    {
        ArgumentNullException.ThrowIfNull(locale);
        return IsValid(locale)
            ? Normalize(locale)
            : throw new ArgumentException($"Locale '{locale}' is not valid; expected a BCP 47 tag such as 'en-US'.", nameof(locale));
    }

    /// <summary>
    /// 规范化大小写：<c>zh-cn</c> → <c>zh-CN</c>，<c>ZH-hant-tw</c> → <c>zh-Hant-TW</c>。输入必须已合法。
    /// </summary>
    public static string Normalize(string locale)
    {
        ArgumentNullException.ThrowIfNull(locale);
        var parts = locale.Split('-');
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = i switch
            {
                // 语言子标签一律小写
                0 => parts[i].ToLowerInvariant(),
                // 四位是文字子标签（Hant、Latn），首字母大写
                _ when parts[i].Length == 4 => char.ToUpperInvariant(parts[i][0]) + parts[i][1..].ToLowerInvariant(),
                // 两位是地区子标签，大写；三位数字地区（419）大小写无关，其余子标签一律小写
                _ when parts[i].Length == 2 => parts[i].ToUpperInvariant(),
                _ => parts[i].ToLowerInvariant(),
            };
        }

        return string.Join('-', parts);
    }

    /// <summary>
    /// 取语言子标签（<c>zh-CN</c> → <c>zh</c>），小写。
    /// </summary>
    public static string LanguageOf(string locale)
    {
        ArgumentNullException.ThrowIfNull(locale);
        var separator = locale.IndexOf('-', StringComparison.Ordinal);
        return (separator < 0 ? locale : locale[..separator]).ToLowerInvariant();
    }

    /// <summary>
    /// 区域对应的文化；运行时不认识的区域（或不变全球化模式）用不变文化。
    /// </summary>
    public static CultureInfo CultureOf(string locale)
    {
        ArgumentNullException.ThrowIfNull(locale);
        try
        {
            return CultureInfo.GetCultureInfo(locale);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    /// <summary>
    /// 两个区域名是否相同（不区分大小写）。
    /// </summary>
    public static bool AreEqual(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 区域名文法：2–3 个字母的语言子标签，之后是 0 到多个 2–8 位字母数字子标签。
    /// </summary>
    [GeneratedRegex("^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$")]
    private static partial Regex Pattern();
}
