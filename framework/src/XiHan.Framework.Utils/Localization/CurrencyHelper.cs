// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;

namespace XiHan.Framework.Utils.Localization;

/// <summary>
/// 货币处理帮助类
/// </summary>
public static class CurrencyHelper
{
    /// <summary>
    /// 货币代码到区域信息的映射
    /// </summary>
    /// <remarks>
    /// 枚举全部特定区域性再构造 <see cref="RegionInfo"/> 的开销不低，而结果在进程内不变，
    /// 因此只算一次。原实现每次查询都重算一遍，<c>FormatCurrency</c> 一次调用就要枚举两轮。
    /// 少数区域性没有对应的国家/地区（构造 <see cref="RegionInfo"/> 会抛异常），按无货币信息跳过。
    /// </remarks>
    private static readonly Lazy<Dictionary<string, RegionInfo>> CurrencyRegions = new(BuildCurrencyRegions);

    /// <summary>
    /// 货币代码到区域性的映射，用于按货币挑选默认的数字格式
    /// </summary>
    private static readonly Lazy<Dictionary<string, CultureInfo>> CurrencyCultures = new(BuildCurrencyCultures);

    /// <summary>
    /// 获取所有货币信息
    /// </summary>
    /// <returns>货币信息列表</returns>
    public static IEnumerable<RegionInfo> GetAllCurrencies()
    {
        return CurrencyRegions.Value.Values;
    }

    /// <summary>
    /// 获取货币信息
    /// </summary>
    /// <param name="currencyCode">货币代码，如 "USD", "CNY"</param>
    /// <returns>货币信息</returns>
    public static RegionInfo GetCurrencyInfo(string currencyCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);

        return CurrencyRegions.Value.TryGetValue(currencyCode, out var region)
            ? region
            : throw new ArgumentException($"Currency code {currencyCode} not found", nameof(currencyCode));
    }

    /// <summary>
    /// 格式化货币金额
    /// </summary>
    /// <param name="amount">金额</param>
    /// <param name="currencyCode">货币代码，决定输出的货币符号与小数位数</param>
    /// <param name="culture">文化，决定数字分组、小数点与符号位置等排版约定</param>
    /// <returns>格式化后的字符串</returns>
    /// <exception cref="ArgumentException">货币代码不存在时抛出</exception>
    /// <remarks>
    /// 原实现只拿 <paramref name="currencyCode"/> 做了一次存在性校验就丢掉，
    /// 实际输出的是 <paramref name="culture"/> 自己的货币，
    /// <c>FormatCurrency(100m, "USD", zh-CN)</c> 会得到 "¥100.00" 这种把美元标成人民币的结果。
    /// 现在按 <paramref name="culture"/> 的排版约定输出，但货币符号与小数位数取自 <paramref name="currencyCode"/>。
    /// </remarks>
    public static string FormatCurrency(decimal amount, string currencyCode, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        var region = GetCurrencyInfo(currencyCode);
        var format = (NumberFormatInfo)culture.NumberFormat.Clone();
        format.CurrencySymbol = region.CurrencySymbol;

        // 小数位数随货币走：日元无小数位，第纳尔有三位，沿用排版文化的位数会算错面额
        if (CurrencyCultures.Value.TryGetValue(region.ISOCurrencySymbol, out var currencyCulture))
        {
            format.CurrencyDecimalDigits = currencyCulture.NumberFormat.CurrencyDecimalDigits;
        }

        return amount.ToString("C", format);
    }

    /// <summary>
    /// 格式化货币金额
    /// </summary>
    /// <param name="amount">金额</param>
    /// <param name="currencyCode">货币代码</param>
    /// <returns>格式化后的字符串</returns>
    public static string FormatCurrency(decimal amount, string currencyCode)
    {
        var culture = GetCurrencyCulture(currencyCode);
        return FormatCurrency(amount, currencyCode, culture);
    }

    /// <summary>
    /// 获取货币文化
    /// </summary>
    /// <param name="currencyCode">货币代码</param>
    /// <returns>文化信息</returns>
    public static CultureInfo GetCurrencyCulture(string currencyCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);

        return CurrencyCultures.Value.TryGetValue(currencyCode, out var culture) ? culture : CultureInfo.CurrentCulture;
    }

    /// <summary>
    /// 获取货币符号
    /// </summary>
    /// <param name="currencyCode">货币代码</param>
    /// <returns>货币符号</returns>
    public static string GetCurrencySymbol(string currencyCode)
    {
        var region = GetCurrencyInfo(currencyCode);
        return region.CurrencySymbol;
    }

    /// <summary>
    /// 获取货币名称
    /// </summary>
    /// <param name="currencyCode">货币代码</param>
    /// <returns>货币名称</returns>
    public static string GetCurrencyName(string currencyCode)
    {
        var region = GetCurrencyInfo(currencyCode);
        return region.CurrencyEnglishName;
    }

    /// <summary>
    /// 获取货币本地名称
    /// </summary>
    /// <param name="currencyCode">货币代码</param>
    /// <param name="_">文化(未使用)</param>
    /// <returns>货币本地名称</returns>
    public static string GetCurrencyNativeName(string currencyCode, CultureInfo _)
    {
        var region = GetCurrencyInfo(currencyCode);
        return region.CurrencyNativeName;
    }

    /// <summary>
    /// 检查货币代码是否存在
    /// </summary>
    /// <param name="currencyCode">货币代码</param>
    /// <returns>是否存在</returns>
    public static bool IsCurrencyCodeExists(string currencyCode)
    {
        return GetAllCurrencies().Any(region => region.ISOCurrencySymbol == currencyCode);
    }

    /// <summary>
    /// 转换货币金额
    /// </summary>
    /// <param name="amount">金额</param>
    /// <param name="_1">源货币代码(未使用)</param>
    /// <param name="_2">目标货币代码(未使用)</param>
    /// <param name="exchangeRate">汇率</param>
    /// <returns>转换后的金额</returns>
    public static decimal ConvertCurrency(decimal amount, string _1, string _2, decimal exchangeRate)
    {
        return amount * exchangeRate;
    }

    /// <summary>
    /// 构建货币代码到区域信息的映射
    /// </summary>
    private static Dictionary<string, RegionInfo> BuildCurrencyRegions()
    {
        var regions = new Dictionary<string, RegionInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var region in EnumerateRegions())
        {
            regions.TryAdd(region.ISOCurrencySymbol, region);
        }

        return regions;
    }

    /// <summary>
    /// 构建货币代码到区域性的映射
    /// </summary>
    private static Dictionary<string, CultureInfo> BuildCurrencyCultures()
    {
        var cultures = new Dictionary<string, CultureInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            if (TryGetRegion(culture, out var region))
            {
                cultures.TryAdd(region!.ISOCurrencySymbol, culture);
            }
        }

        return cultures;
    }

    /// <summary>
    /// 枚举所有能取到国家/地区信息的区域性对应的区域
    /// </summary>
    private static IEnumerable<RegionInfo> EnumerateRegions()
    {
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            if (TryGetRegion(culture, out var region))
            {
                yield return region!;
            }
        }
    }

    /// <summary>
    /// 尝试取区域性对应的国家/地区信息
    /// </summary>
    /// <remarks>伪区域性等少数区域性没有对应的国家/地区，构造时会抛异常，这里按"无货币信息"跳过。</remarks>
    private static bool TryGetRegion(CultureInfo culture, out RegionInfo? region)
    {
        try
        {
            region = new RegionInfo(culture.Name);
            return true;
        }
        catch (ArgumentException)
        {
            region = null;
            return false;
        }
    }
}
