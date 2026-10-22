// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Abstractions;

/// <summary>
/// 本地化选项。从配置节 <c>XiHan:Localization</c> 显式读取。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record LocalizationOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Localization";

    /// <summary>
    /// 默认区域；协商不到时使用。默认 <see cref="SupportedLocales.Default"/>（英文）。
    /// </summary>
    public string DefaultLocale
    {
        get => field;
        init => field = LocaleName.Validate(value);
    } = SupportedLocales.Default;

    /// <summary>
    /// 启用的区域，按优先级排列；默认是内置的 10 种。
    /// </summary>
    public IReadOnlyList<string> Locales
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Count == 0)
            {
                throw new ArgumentException("At least one locale must be enabled.", nameof(value));
            }

            field = value.Select(LocaleName.Validate).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    } = SupportedLocales.All;

    /// <summary>
    /// 从配置读取：<c>DefaultLocale</c>、<c>Locales</c>（逗号分隔）。默认区域必须在启用列表里。
    /// </summary>
    public static LocalizationOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new LocalizationOptions();
        if (configuration[$"{ConfigurationSection}:Locales"] is { } locales)
        {
            options = options with { Locales = locales.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) };
        }

        if (configuration[$"{ConfigurationSection}:DefaultLocale"] is { } defaultLocale)
        {
            options = options with { DefaultLocale = defaultLocale };
        }

        return options.Validated();
    }

    /// <summary>
    /// 校验组合约束：默认区域必须在启用列表里。
    /// </summary>
    public LocalizationOptions Validated()
        => Locales.Any(l => LocaleName.AreEqual(l, DefaultLocale))
            ? this
            : throw new ArgumentException($"Default locale '{DefaultLocale}' is not in the enabled locales.");

    /// <summary>
    /// 从候选区域（按偏好顺序）协商出启用的区域：先精确匹配，再按语言匹配（<c>zh</c> 或 <c>zh-HK</c> 命中第一个 <c>zh-*</c>），都没有则用默认区域。
    /// </summary>
    public string Negotiate(IEnumerable<string?> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var list = candidates.Where(static c => LocaleName.IsValid(c)).Select(static c => c!).ToList();

        // 第一轮：任一候选精确命中启用列表
        foreach (var candidate in list)
        {
            var exact = Locales.FirstOrDefault(l => LocaleName.AreEqual(l, candidate));
            if (exact is not null)
            {
                return exact;
            }
        }

        // 第二轮：按语言子标签命中启用列表里第一个同语言的区域
        foreach (var candidate in list)
        {
            var language = LocaleName.LanguageOf(candidate);
            var sameLanguage = Locales.FirstOrDefault(l => string.Equals(LocaleName.LanguageOf(l), language, StringComparison.Ordinal));
            if (sameLanguage is not null)
            {
                return sameLanguage;
            }
        }

        return DefaultLocale;
    }

    /// <summary>
    /// 解析调用的区域：Baggage <see cref="BaggageKeys.Locale"/> 经协商，缺失时为默认区域。
    /// </summary>
    public string Resolve(InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.TryGetBaggage(BaggageKeys.Locale, out var locale) ? Negotiate([locale]) : DefaultLocale;
    }
}
