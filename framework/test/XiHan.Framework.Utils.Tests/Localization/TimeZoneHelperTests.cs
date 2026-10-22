// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Localization;

namespace XiHan.Framework.Utils.Tests.Localization;

/// <summary>
/// 时区解析测试
/// </summary>
/// <remarks>
/// 新版 tzdata 不再带 Asia/Calcutta、America/Indianapolis 这类旧别名，原实现直接 FindSystemTimeZoneById 就解析失败。
/// 落点规则用只有规范 ID 的模拟目录来核，不依赖本机有没有别名文件；
/// Windows ID 与 IANA ID 的对应关系来自 ICU，运行环境没有 ICU 时相关用例跳过。
/// </remarks>
public class TimeZoneHelperTests
{
    /// <summary>
    /// 探测运行环境能否在 IANA ID 与 Windows ID 之间互转，探测一次后缓存
    /// </summary>
    private static readonly Lazy<bool> IcuMappingAvailable = new(() => TimeZoneInfo.TryConvertIanaIdToWindowsId("Asia/Shanghai", out _));

    /// <summary>
    /// 探测运行环境能否解析 IANA 时区标识，探测一次后缓存
    /// </summary>
    private static readonly Lazy<bool> IanaTimeZoneAvailable = new(() => TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Shanghai", out _));

    /// <summary>
    /// 新版 tzdata 的 Unix 时区目录：只有规范 IANA ID，没有旧别名
    /// </summary>
    private static readonly string[] CanonicalOnlyIds =
    [
        "America/Argentina/Buenos_Aires",
        "America/Indiana/Indianapolis",
        "America/Indiana/Marengo",
        "America/New_York",
        "Asia/Kathmandu",
        "Asia/Kolkata",
        "Asia/Shanghai",
        "Europe/Helsinki",
        "Europe/Kyiv",
        "Europe/Riga"
    ];

    /// <summary>
    /// 旧 IANA 别名与 Windows ID 落到同一个 Windows 时区下 ID 按序数排在最前的已安装时区
    /// </summary>
    [Theory]
    [InlineData("America/Indianapolis", "America/Indiana/Indianapolis")]
    [InlineData("US Eastern Standard Time", "America/Indiana/Indianapolis")]
    [InlineData("Asia/Calcutta", "Asia/Kolkata")]
    [InlineData("India Standard Time", "Asia/Kolkata")]
    [InlineData("Europe/Kiev", "Europe/Helsinki")]
    [InlineData("FLE Standard Time", "Europe/Helsinki")]
    [InlineData("Asia/Katmandu", "Asia/Kathmandu")]
    [InlineData("America/Buenos_Aires", "America/Argentina/Buenos_Aires")]
    [InlineData("China Standard Time", "Asia/Shanghai")]
    public void TryResolve_OnCanonicalOnlyCatalog_ResolvesLegacyAliasAndWindowsId(string timeZoneId, string expectedId)
    {
        Assert.SkipUnless(IcuMappingAvailable.Value, "当前运行环境没有 ICU 时区映射，跳过该组验证。");

        var resolver = CreateUnixResolver(CanonicalOnlyIds);

        Assert.True(resolver.TryResolve(timeZoneId, out var timeZone));
        Assert.Equal(expectedId, timeZone.Id);
    }

    /// <summary>
    /// 目录里本来就有的规范 ID 直接命中，前后空白被忽略
    /// </summary>
    [Theory]
    [InlineData("Asia/Shanghai")]
    [InlineData("  Asia/Shanghai  ")]
    public void TryResolve_WithInstalledId_ReturnsItDirectly(string timeZoneId)
    {
        var resolver = CreateUnixResolver(CanonicalOnlyIds);

        Assert.True(resolver.TryResolve(timeZoneId, out var timeZone));
        Assert.Equal("Asia/Shanghai", timeZone.Id);
    }

    /// <summary>
    /// 同组时区的选取不随目录枚举顺序变化
    /// </summary>
    [Theory]
    [InlineData("America/Indianapolis", "America/Indiana/Indianapolis")]
    [InlineData("Europe/Kiev", "Europe/Helsinki")]
    public void TryResolve_WithReversedCatalog_PicksSameZone(string timeZoneId, string expectedId)
    {
        Assert.SkipUnless(IcuMappingAvailable.Value, "当前运行环境没有 ICU 时区映射，跳过该组验证。");

        var resolver = CreateUnixResolver(Enumerable.Reverse(CanonicalOnlyIds));

        Assert.True(resolver.TryResolve(timeZoneId, out var timeZone));
        Assert.Equal(expectedId, timeZone.Id);
    }

    /// <summary>
    /// 不属于任何 Windows 时区、或目录里没有同组时区的标识解析失败，不随便落到一个时区
    /// </summary>
    [Theory]
    [InlineData("Mars/Olympus_Mons")]
    [InlineData("Tokyo Standard Time")]
    [InlineData("Asia/Tokyo")]
    public void TryResolve_WithoutEquivalentZone_ReturnsFalse(string timeZoneId)
    {
        var resolver = CreateUnixResolver(CanonicalOnlyIds);

        Assert.False(resolver.TryResolve(timeZoneId, out var timeZone));
        Assert.Null(timeZone);
    }

    /// <summary>
    /// 空标识直接失败，不查目录
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryResolve_WithEmptyId_ReturnsFalse(string? timeZoneId)
    {
        var resolver = CreateUnixResolver(CanonicalOnlyIds);

        Assert.False(resolver.TryResolve(timeZoneId, out var timeZone));
        Assert.Null(timeZone);
    }

    /// <summary>
    /// 按 Windows 时区归组的索引只在前两步都失败时才建，且只建一次
    /// </summary>
    [Fact]
    public void TryResolve_BuildsWindowsZoneIndexLazilyAndOnce()
    {
        Assert.SkipUnless(IcuMappingAvailable.Value, "当前运行环境没有 ICU 时区映射，跳过该组验证。");

        var catalog = CreateCatalog(CanonicalOnlyIds);
        var listCount = 0;
        var resolver = new TimeZoneIdResolver(
            id => catalog.FirstOrDefault(zone => zone.Id == id),
            () =>
            {
                listCount++;
                return catalog;
            },
            isWindows: false);

        Assert.True(resolver.TryResolve("Asia/Shanghai", out _));
        Assert.Equal(0, listCount);

        Assert.True(resolver.TryResolve("Asia/Calcutta", out _));
        Assert.True(resolver.TryResolve("Europe/Kiev", out _));
        Assert.False(resolver.TryResolve("Asia/Tokyo", out _));
        Assert.Equal(1, listCount);
    }

    /// <summary>
    /// Windows 目录上 IANA ID（含旧别名）先转成 Windows ID 再找
    /// </summary>
    [Theory]
    [InlineData("Asia/Calcutta", "India Standard Time")]
    [InlineData("Asia/Kolkata", "India Standard Time")]
    [InlineData("Asia/Shanghai", "China Standard Time")]
    public void TryResolve_OnWindowsCatalog_ConvertsIanaIdToWindowsId(string timeZoneId, string expectedId)
    {
        Assert.SkipUnless(IcuMappingAvailable.Value, "当前运行环境没有 ICU 时区映射，跳过该组验证。");

        var catalog = CreateCatalog(["China Standard Time", "India Standard Time"]);
        var resolver = new TimeZoneIdResolver(id => catalog.FirstOrDefault(zone => zone.Id == id), () => catalog, isWindows: true);

        Assert.True(resolver.TryResolve(timeZoneId, out var timeZone));
        Assert.Equal(expectedId, timeZone.Id);
    }

    /// <summary>
    /// 本机目录上旧别名、Windows ID 与规范 ID 换算出同一个偏移
    /// </summary>
    [Theory]
    [InlineData("Asia/Kolkata")]
    [InlineData("Asia/Calcutta")]
    [InlineData("India Standard Time")]
    public void GetTimeZone_OnSystemCatalog_ResolvesLegacyAliasAndWindowsId(string timeZoneId)
    {
        Assert.SkipUnless(IanaTimeZoneAvailable.Value && IcuMappingAvailable.Value, "当前运行环境无法解析 IANA 时区或没有 ICU 时区映射，跳过该组验证。");

        var timeZone = TimeZoneHelper.GetTimeZone(timeZoneId);

        Assert.Equal(TimeSpan.FromMinutes(330), timeZone.GetUtcOffset(new DateTimeOffset(2024, 5, 6, 0, 0, 0, TimeSpan.Zero)));
        Assert.True(TimeZoneHelper.IsTimeZoneExists(timeZoneId));
    }

    /// <summary>
    /// 找不到的时区：GetTimeZone 抛 TimeZoneNotFoundException，Try 与 IsTimeZoneExists 返回 false
    /// </summary>
    [Fact]
    public void GetTimeZone_WhenNotFound_ThrowsTimeZoneNotFoundException()
    {
        Assert.Throws<TimeZoneNotFoundException>(() => TimeZoneHelper.GetTimeZone("不存在的时区"));
        Assert.Throws<ArgumentNullException>(() => TimeZoneHelper.GetTimeZone(null!));
        Assert.False(TimeZoneHelper.TryGetTimeZone("不存在的时区", out _));
        Assert.False(TimeZoneHelper.TryGetTimeZone(null, out _));
        Assert.False(TimeZoneHelper.IsTimeZoneExists("不存在的时区"));
    }

    /// <summary>
    /// 用模拟目录构造一个按 Unix 规则解析的解析器，按 ID 直接查找分大小写，与 Unix 文件系统一致
    /// </summary>
    /// <param name="ids">目录里的时区 ID</param>
    private static TimeZoneIdResolver CreateUnixResolver(IEnumerable<string> ids)
    {
        var catalog = CreateCatalog(ids);
        return new TimeZoneIdResolver(id => catalog.FirstOrDefault(zone => zone.Id == id), () => catalog, isWindows: false);
    }

    /// <summary>
    /// 构造模拟时区目录，只有 ID 参与解析
    /// </summary>
    /// <param name="ids">目录里的时区 ID</param>
    private static TimeZoneInfo[] CreateCatalog(IEnumerable<string> ids)
    {
        return [.. ids.Select(id => TimeZoneInfo.CreateCustomTimeZone(id, TimeSpan.Zero, id, id))];
    }
}
