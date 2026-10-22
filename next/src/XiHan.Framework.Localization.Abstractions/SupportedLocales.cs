// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Abstractions;

/// <summary>
/// 框架内置支持的 10 种区域，与 XiHan.UI 的语言包一致。默认区域是 <see cref="Default"/>（英文）。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class SupportedLocales
{
    /// <summary>英语（美国）；默认区域，也是代码里消息文本的语言。</summary>
    public const string EnglishUnitedStates = "en-US";

    /// <summary>简体中文。</summary>
    public const string ChineseSimplified = "zh-CN";

    /// <summary>繁体中文（台湾）。</summary>
    public const string ChineseTraditional = "zh-TW";

    /// <summary>日语。</summary>
    public const string Japanese = "ja-JP";

    /// <summary>韩语。</summary>
    public const string Korean = "ko-KR";

    /// <summary>德语。</summary>
    public const string German = "de-DE";

    /// <summary>西班牙语。</summary>
    public const string Spanish = "es-ES";

    /// <summary>法语。</summary>
    public const string French = "fr-FR";

    /// <summary>葡萄牙语（巴西）。</summary>
    public const string PortugueseBrazil = "pt-BR";

    /// <summary>俄语。</summary>
    public const string Russian = "ru-RU";

    /// <summary>
    /// 默认区域。
    /// </summary>
    public const string Default = EnglishUnitedStates;

    /// <summary>
    /// 全部内置区域，默认区域在前。
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        EnglishUnitedStates,
        ChineseSimplified,
        ChineseTraditional,
        Japanese,
        Korean,
        German,
        Spanish,
        French,
        PortugueseBrazil,
        Russian,
    ];
}
