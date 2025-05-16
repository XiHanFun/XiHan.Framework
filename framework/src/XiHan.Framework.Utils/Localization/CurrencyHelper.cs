#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CurrencyHelper
// Guid:9b8c7d6e-5f4e-3b2c-1a9d-8e7f6d5c4b3a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/29 0:47:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Globalization;

namespace XiHan.Framework.Utils.Localization;

/// <summary>
/// 货币处理帮助类
/// </summary>
public static class CurrencyHelper
{
    /// <summary>
    /// 获取所有货币信息
    /// </summary>
    /// <returns>货币信息列表</returns>
    public static IEnumerable<RegionInfo> GetAllCurrencies()
    {
        return CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Select(culture => new RegionInfo(culture.Name))
            .DistinctBy(region => region.ISOCurrencySymbol);
    }

    /// <summary>
    /// 获取货币信息
    /// </summary>
    /// <param name="currencyCode">货币代码，如 "USD", "CNY"</param>
    /// <returns>货币信息</returns>
    public static RegionInfo GetCurrencyInfo(string currencyCode)
    {
        return GetAllCurrencies().FirstOrDefault(region => region.ISOCurrencySymbol == currencyCode)
               ?? throw new ArgumentException($"Currency code {currencyCode} not found");
    }

    /// <summary>
    /// 格式化货币金额
    /// </summary>
    /// <param name="amount">金额</param>
    /// <param name="currencyCode">货币代码</param>
    /// <param name="culture">文化</param>
    /// <returns>格式化后的字符串</returns>
    public static string FormatCurrency(decimal amount, string currencyCode, CultureInfo culture)
    {
        _ = GetCurrencyInfo(currencyCode); // Validate currency code exists
        return amount.ToString("C", culture);
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
        return CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .FirstOrDefault(culture => new RegionInfo(culture.Name).ISOCurrencySymbol == currencyCode)
            ?? CultureInfo.CurrentCulture;
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
    /// <param name="_">文化（未使用）</param>
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
    /// <param name="_1">源货币代码（未使用）</param>
    /// <param name="_2">目标货币代码（未使用）</param>
    /// <param name="exchangeRate">汇率</param>
    /// <returns>转换后的金额</returns>
    public static decimal ConvertCurrency(decimal amount, string _1, string _2, decimal exchangeRate)
    {
        return amount * exchangeRate;
    }
}
