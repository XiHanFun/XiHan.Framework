// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core;

namespace XiHan.Framework.Timing.Tests;

/// <summary>
/// 时区提供器测试
/// </summary>
/// <remarks>
/// 该实现是 TimeZoneConverter 的薄封装，因此断言只锁死本项目自己承担的那部分契约：
/// 列表的排序、过滤规则与名值同源，以及解析失败时异常必须原样冒泡（不能被吞成 null）。
/// 具体的映射关系只挑 CLDR 中长期稳定的几组做样本，不去枚举整张映射表。
/// </remarks>
public class TZConvertTimezoneProviderTests
{
    private const string ChinaWindowsTimeZoneId = "China Standard Time";
    private const string ShanghaiIanaTimeZoneName = "Asia/Shanghai";

    /// <summary>
    /// Windows 时区列表非空，且名与值同源
    /// </summary>
    [Fact]
    public void GetWindowsTimezones_ReturnsPairsWhoseNameEqualsValue()
    {
        var provider = new TZConvertTimezoneProvider();

        var timezones = provider.GetWindowsTimezones();

        Assert.NotEmpty(timezones);
        Assert.All(timezones, item => Assert.Equal(item.Name, item.Value));
        Assert.All(timezones, item => Assert.False(string.IsNullOrWhiteSpace(item.Name)));
    }

    /// <summary>
    /// Windows 时区列表按名称升序排列
    /// </summary>
    [Fact]
    public void GetWindowsTimezones_ReturnsNamesInAscendingOrder()
    {
        var provider = new TZConvertTimezoneProvider();

        var names = provider.GetWindowsTimezones().Select(item => item.Name).ToList();

        Assert.Equal(names.OrderBy(name => name).ToList(), names);
    }

    /// <summary>
    /// Windows 时区列表包含常用时区
    /// </summary>
    [Fact]
    public void GetWindowsTimezones_ContainsWellKnownWindowsIds()
    {
        var provider = new TZConvertTimezoneProvider();

        var names = provider.GetWindowsTimezones().Select(item => item.Name).ToList();

        Assert.Contains(ChinaWindowsTimeZoneId, names);
        Assert.Contains("UTC", names);
    }

    /// <summary>
    /// IANA 时区列表只保留带区域前缀的名称，外加裸的 UTC
    /// </summary>
    [Fact]
    public void GetIanaTimezones_KeepsOnlyRegionQualifiedNamesPlusUtc()
    {
        var provider = new TZConvertTimezoneProvider();

        var timezones = provider.GetIanaTimezones();

        Assert.NotEmpty(timezones);
        Assert.All(timezones, item => Assert.True(
            item.Name == "UTC" || (item.Name.Contains('/') && !item.Name.Contains("Etc")),
            $"意外的 IANA 时区名称：{item.Name}"));
        Assert.All(timezones, item => Assert.Equal(item.Name, item.Value));
    }

    /// <summary>
    /// IANA 时区列表排除 Etc 系列，但保留 UTC 与常用区域时区
    /// </summary>
    [Fact]
    public void GetIanaTimezones_ExcludesEtcZonesButKeepsUtcAndRegionZones()
    {
        var provider = new TZConvertTimezoneProvider();

        var names = provider.GetIanaTimezones().Select(item => item.Name).ToList();

        Assert.Contains("UTC", names);
        Assert.Contains(ShanghaiIanaTimeZoneName, names);
        Assert.DoesNotContain("Etc/UTC", names);
        Assert.DoesNotContain("Etc/GMT", names);
    }

    /// <summary>
    /// IANA 时区列表按名称升序排列
    /// </summary>
    [Fact]
    public void GetIanaTimezones_ReturnsNamesInAscendingOrder()
    {
        var provider = new TZConvertTimezoneProvider();

        var names = provider.GetIanaTimezones().Select(item => item.Name).ToList();

        Assert.Equal(names.OrderBy(name => name).ToList(), names);
    }

    /// <summary>
    /// 两份列表各自独立，不共享同一批对象
    /// </summary>
    /// <remarks>
    /// 返回的是可变的 <see cref="List{T}"/>，调用方改动不应污染下一次调用的结果。
    /// </remarks>
    [Fact]
    public void GetWindowsTimezones_ReturnsFreshListOnEachCall()
    {
        var provider = new TZConvertTimezoneProvider();

        var first = provider.GetWindowsTimezones();
        var originalCount = first.Count;
        first.Add(new NameValue("XiHan/Fake", "XiHan/Fake"));
        var second = provider.GetWindowsTimezones();

        Assert.Equal(originalCount, second.Count);
        Assert.DoesNotContain(second, item => item.Name == "XiHan/Fake");
    }

    /// <summary>
    /// Windows 时区标识可转换为对应的 IANA 名称
    /// </summary>
    [Fact]
    public void WindowsToIana_ForKnownWindowsId_ReturnsIanaName()
    {
        var provider = new TZConvertTimezoneProvider();

        Assert.Equal(ShanghaiIanaTimeZoneName, provider.WindowsToIana(ChinaWindowsTimeZoneId));
    }

    /// <summary>
    /// IANA 名称可转换为对应的 Windows 时区标识
    /// </summary>
    [Fact]
    public void IanaToWindows_ForKnownIanaName_ReturnsWindowsId()
    {
        var provider = new TZConvertTimezoneProvider();

        Assert.Equal(ChinaWindowsTimeZoneId, provider.IanaToWindows(ShanghaiIanaTimeZoneName));
    }

    /// <summary>
    /// 常用时区在两种命名体系之间可以往返
    /// </summary>
    [Theory]
    [InlineData("China Standard Time")]
    [InlineData("Tokyo Standard Time")]
    [InlineData("Pacific Standard Time")]
    [InlineData("GMT Standard Time")]
    public void WindowsToIana_ThenIanaToWindows_RoundTripsBackToSameWindowsId(string windowsTimeZoneId)
    {
        var provider = new TZConvertTimezoneProvider();

        var ianaTimeZoneName = provider.WindowsToIana(windowsTimeZoneId);

        Assert.Contains("/", ianaTimeZoneName);
        Assert.Equal(windowsTimeZoneId, provider.IanaToWindows(ianaTimeZoneName));
    }

    /// <summary>
    /// 无法识别的 Windows 时区标识直接抛出，不返回空值
    /// </summary>
    [Fact]
    public void WindowsToIana_WhenWindowsIdUnknown_ThrowsInvalidTimeZoneException()
    {
        var provider = new TZConvertTimezoneProvider();

        Assert.Throws<InvalidTimeZoneException>(() => provider.WindowsToIana("XiHan Not A Real Zone"));
    }

    /// <summary>
    /// 无法识别的 IANA 名称直接抛出，不返回空值
    /// </summary>
    [Fact]
    public void IanaToWindows_WhenIanaNameUnknown_ThrowsInvalidTimeZoneException()
    {
        var provider = new TZConvertTimezoneProvider();

        Assert.Throws<InvalidTimeZoneException>(() => provider.IanaToWindows("XiHan/NotARealZone"));
    }

    /// <summary>
    /// 时区信息解析同时接受 Windows 与 IANA 两种标识，结果偏移一致
    /// </summary>
    [Fact]
    public void GetTimeZoneInfo_AcceptsBothWindowsAndIanaIds_ResolvesToSameOffset()
    {
        var provider = new TZConvertTimezoneProvider();

        var fromWindowsId = provider.GetTimeZoneInfo(ChinaWindowsTimeZoneId);
        var fromIanaName = provider.GetTimeZoneInfo(ShanghaiIanaTimeZoneName);

        Assert.Equal(TimeSpan.FromHours(8), fromWindowsId.BaseUtcOffset);
        Assert.Equal(TimeSpan.FromHours(8), fromIanaName.BaseUtcOffset);
    }

    /// <summary>
    /// 解析出的时区可直接用于换算
    /// </summary>
    [Fact]
    public void GetTimeZoneInfo_ResolvedZone_CanConvertUtcToWallClock()
    {
        var provider = new TZConvertTimezoneProvider();
        var utcDateTime = new DateTime(2024, 3, 15, 2, 0, 0, DateTimeKind.Utc);

        var converted = TimeZoneInfo.ConvertTime(utcDateTime, provider.GetTimeZoneInfo(ShanghaiIanaTimeZoneName));

        // 只比刻度：换算结果的 Kind 取决于目标时区是否恰好等于运行机器的本地时区，不属于本项目契约
        Assert.Equal(new DateTime(2024, 3, 15, 10, 0, 0).Ticks, converted.Ticks);
    }

    /// <summary>
    /// 无法识别的时区标识必须抛出，不能静默降级为 UTC
    /// </summary>
    [Fact]
    public void GetTimeZoneInfo_WhenIdUnknown_Throws()
    {
        var provider = new TZConvertTimezoneProvider();

        Assert.ThrowsAny<Exception>(() => provider.GetTimeZoneInfo("XiHan/NotARealZone"));
    }
}
