// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Localization;

namespace XiHan.Framework.Localization.Abstractions;

/// <summary>
/// 可本地化字符串扩展
/// </summary>
public static class LocalizableStringExtensions
{
    /// <summary>
    /// 本地化并在缺失时回退到默认值
    /// </summary>
    /// <param name="localizableString">可本地化字符串</param>
    /// <param name="stringLocalizerFactory">本地化工厂</param>
    /// <param name="fallback">缺失时回退值</param>
    /// <returns></returns>
    public static string LocalizeOrFallback(
        this ILocalizableString? localizableString,
        IStringLocalizerFactory stringLocalizerFactory,
        string? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(stringLocalizerFactory);

        if (localizableString is null)
        {
            return fallback ?? string.Empty;
        }

        var localized = localizableString.Localize(stringLocalizerFactory);
        if (localized.ResourceNotFound && !string.IsNullOrWhiteSpace(fallback))
        {
            return fallback;
        }

        return localized.Value;
    }

    /// <summary>
    /// 获取对象显示名称（优先使用本地化名称）
    /// </summary>
    /// <param name="source">对象</param>
    /// <param name="stringLocalizerFactory">本地化工厂</param>
    /// <returns></returns>
    public static string GetLocalizedDisplayName(
        this IHasNameWithLocalizableDisplayName source,
        IStringLocalizerFactory stringLocalizerFactory)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(stringLocalizerFactory);

        return source.DisplayName.LocalizeOrFallback(stringLocalizerFactory, source.Name);
    }

    /// <summary>
    /// 创建固定文本本地化字符串
    /// </summary>
    /// <param name="value">固定文本</param>
    /// <returns></returns>
    public static ILocalizableString ToFixedLocalizableString(this string value)
    {
        return new FixedLocalizableString(value);
    }
}
