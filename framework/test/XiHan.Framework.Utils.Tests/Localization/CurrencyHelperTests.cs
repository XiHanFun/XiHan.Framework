// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using XiHan.Framework.Utils.Localization;

namespace XiHan.Framework.Utils.Tests.Localization;

/// <summary>
/// 货币处理帮助类测试
/// </summary>
/// <remarks>
/// 原实现的 <c>FormatCurrency</c> 只拿货币代码做了一次存在性校验就丢掉，
/// 实际输出的是排版文化自己的货币，把美元标成人民币也不会报错。
/// 这里锁住"货币符号与小数位数跟着货币代码走、数字排版跟着文化走"。
/// </remarks>
public class CurrencyHelperTests
{
    /// <summary>
    /// 输出的货币符号由货币代码决定，与排版文化无关
    /// </summary>
    [Theory]
    [InlineData("USD", "zh-CN", "$")]
    [InlineData("USD", "en-US", "$")]
    [InlineData("CNY", "en-US", "¥")]
    [InlineData("EUR", "en-US", "€")]
    public void FormatCurrency_UsesSymbolOfRequestedCurrency(string currencyCode, string cultureName, string expectedSymbol)
    {
        var formatted = CurrencyHelper.FormatCurrency(1234.5m, currencyCode, new CultureInfo(cultureName));

        Assert.Contains(expectedSymbol, formatted);
    }

    /// <summary>
    /// 同一金额换不同货币会得到不同的货币符号
    /// </summary>
    /// <remarks>原实现下这两次调用的结果完全相同，因为货币代码根本没参与格式化。</remarks>
    [Fact]
    public void FormatCurrency_WithDifferentCurrencies_ProducesDifferentOutput()
    {
        var culture = new CultureInfo("zh-CN");

        var usd = CurrencyHelper.FormatCurrency(1234.5m, "USD", culture);
        var cny = CurrencyHelper.FormatCurrency(1234.5m, "CNY", culture);

        Assert.NotEqual(usd, cny);
        Assert.Contains("$", usd);
    }

    /// <summary>
    /// 小数位数跟着货币走：日元没有小数位
    /// </summary>
    [Fact]
    public void FormatCurrency_UsesDecimalDigitsOfRequestedCurrency()
    {
        var culture = new CultureInfo("zh-CN");

        Assert.DoesNotContain(".", CurrencyHelper.FormatCurrency(1234.5m, "JPY", culture));
        Assert.Contains(".50", CurrencyHelper.FormatCurrency(1234.5m, "USD", culture));
    }

    /// <summary>
    /// 数字分组与小数点排版跟着文化走
    /// </summary>
    [Fact]
    public void FormatCurrency_UsesNumberLayoutOfRequestedCulture()
    {
        var formatted = CurrencyHelper.FormatCurrency(1234.5m, "EUR", new CultureInfo("de-DE"));

        // 德语用点分组、逗号作小数点
        Assert.Contains("1.234,50", formatted);
    }

    /// <summary>
    /// 不存在的货币代码抛参数异常
    /// </summary>
    [Theory]
    [InlineData("XXXX")]
    [InlineData("not-a-currency")]
    public void FormatCurrency_WhenCurrencyCodeUnknown_Throws(string currencyCode)
    {
        Assert.Throws<ArgumentException>(() => CurrencyHelper.FormatCurrency(1m, currencyCode, CultureInfo.InvariantCulture));
        Assert.Throws<ArgumentException>(() => CurrencyHelper.GetCurrencyInfo(currencyCode));
    }

    /// <summary>
    /// 空白的货币代码抛参数异常
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetCurrencyInfo_WhenCurrencyCodeBlank_Throws(string currencyCode)
    {
        Assert.Throws<ArgumentException>(() => CurrencyHelper.GetCurrencyInfo(currencyCode));
    }

    /// <summary>
    /// 货币代码为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void GetCurrencyInfo_WhenCurrencyCodeIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CurrencyHelper.GetCurrencyInfo(null!));
    }

    /// <summary>
    /// 常见货币代码都能查到符号与名称
    /// </summary>
    [Theory]
    [InlineData("USD")]
    [InlineData("CNY")]
    [InlineData("EUR")]
    [InlineData("JPY")]
    public void GetCurrencyInfo_ForCommonCurrencies_ReturnsRegion(string currencyCode)
    {
        Assert.Equal(currencyCode, CurrencyHelper.GetCurrencyInfo(currencyCode).ISOCurrencySymbol);
        Assert.True(CurrencyHelper.IsCurrencyCodeExists(currencyCode));
        Assert.NotEmpty(CurrencyHelper.GetCurrencySymbol(currencyCode));
        Assert.NotEmpty(CurrencyHelper.GetCurrencyName(currencyCode));
    }

    /// <summary>
    /// 货币列表按货币代码去重
    /// </summary>
    [Fact]
    public void GetAllCurrencies_IsDistinctByCurrencyCode()
    {
        var currencies = CurrencyHelper.GetAllCurrencies().ToList();

        Assert.NotEmpty(currencies);
        Assert.Equal(currencies.Count, currencies.Select(region => region.ISOCurrencySymbol).Distinct().Count());
    }

    /// <summary>
    /// 重复调用返回同一批缓存对象
    /// </summary>
    /// <remarks>原实现每次查询都重新枚举全部区域性并构造 RegionInfo，一次格式化要枚举两轮。</remarks>
    [Fact]
    public void GetAllCurrencies_ReusesCachedRegions()
    {
        Assert.Same(CurrencyHelper.GetCurrencyInfo("USD"), CurrencyHelper.GetCurrencyInfo("USD"));
    }
}
