// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Traffic.GrayRouting.Enums;
using XiHan.Framework.Traffic.GrayRouting.Matchers;
using XiHan.Framework.Traffic.GrayRouting.Models;
using XiHan.Framework.Traffic.Tests.Fakes;

namespace XiHan.Framework.Traffic.Tests.GrayRouting.Matchers;

/// <summary>
/// IP 地址灰度匹配器测试
/// </summary>
/// <remarks>
/// 重点是网段边界：/24 的首尾地址都必须落在网内，相邻网段的地址必须落在网外；
/// /32 退化为精确匹配，/0 覆盖整个地址族；跨地址族（IPv4 客户端对 IPv6 网段）一律不命中。
/// </remarks>
public class IpAddressGrayMatcherTests
{
    /// <summary>
    /// 匹配器声明的规则类型是 IP 地址
    /// </summary>
    [Fact]
    public void RuleType_IsIpAddress()
    {
        Assert.Equal(GrayRuleType.IpAddress, new IpAddressGrayMatcher().RuleType);
    }

    /// <summary>
    /// 精确 IP 配置只命中完全相同的地址
    /// </summary>
    [Theory]
    [InlineData("192.168.1.10", true)]
    [InlineData("192.168.1.11", false)]
    [InlineData("192.168.2.10", false)]
    [InlineData("10.0.0.1", false)]
    public void IsMatch_ExactIpEntry_MatchesOnlyIdenticalAddress(string clientIp, bool expected)
    {
        var context = new GrayContext { ClientIpAddress = clientIp };

        Assert.Equal(expected, new IpAddressGrayMatcher().IsMatch(context, CreateRule("""{"IpAddresses":["192.168.1.10"]}""")));
    }

    /// <summary>
    /// /24 网段包含首地址、尾地址与中间地址
    /// </summary>
    [Theory]
    [InlineData("192.168.1.0")]
    [InlineData("192.168.1.1")]
    [InlineData("192.168.1.128")]
    [InlineData("192.168.1.254")]
    [InlineData("192.168.1.255")]
    public void IsMatch_Cidr24_ContainsEveryAddressInsideNetwork(string clientIp)
    {
        var context = new GrayContext { ClientIpAddress = clientIp };

        Assert.True(new IpAddressGrayMatcher().IsMatch(context, CreateRule("""{"IpAddresses":["192.168.1.0/24"]}""")));
    }

    /// <summary>
    /// /24 网段不包含相邻网段的地址
    /// </summary>
    [Theory]
    [InlineData("192.168.0.255")]
    [InlineData("192.168.2.0")]
    [InlineData("192.168.2.1")]
    [InlineData("193.168.1.1")]
    public void IsMatch_Cidr24_ExcludesAddressesOutsideNetwork(string clientIp)
    {
        var context = new GrayContext { ClientIpAddress = clientIp };

        Assert.False(new IpAddressGrayMatcher().IsMatch(context, CreateRule("""{"IpAddresses":["192.168.1.0/24"]}""")));
    }

    /// <summary>
    /// /8 大网段按首字节判定归属
    /// </summary>
    [Theory]
    [InlineData("10.0.0.0", true)]
    [InlineData("10.1.2.3", true)]
    [InlineData("10.255.255.255", true)]
    [InlineData("11.0.0.0", false)]
    [InlineData("9.255.255.255", false)]
    public void IsMatch_Cidr8_MatchesWholePrivateBlock(string clientIp, bool expected)
    {
        var context = new GrayContext { ClientIpAddress = clientIp };

        Assert.Equal(expected, new IpAddressGrayMatcher().IsMatch(context, CreateRule("""{"IpAddresses":["10.0.0.0/8"]}""")));
    }

    /// <summary>
    /// /32 网段退化为精确匹配
    /// </summary>
    [Theory]
    [InlineData("203.0.113.7", true)]
    [InlineData("203.0.113.8", false)]
    [InlineData("203.0.113.6", false)]
    public void IsMatch_Cidr32_BehavesLikeExactMatch(string clientIp, bool expected)
    {
        var context = new GrayContext { ClientIpAddress = clientIp };

        Assert.Equal(expected, new IpAddressGrayMatcher().IsMatch(context, CreateRule("""{"IpAddresses":["203.0.113.7/32"]}""")));
    }

    /// <summary>
    /// /0 网段覆盖整个 IPv4 地址族
    /// </summary>
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("127.0.0.1")]
    [InlineData("255.255.255.255")]
    public void IsMatch_CidrZero_MatchesAnyIpv4Address(string clientIp)
    {
        var context = new GrayContext { ClientIpAddress = clientIp };

        Assert.True(new IpAddressGrayMatcher().IsMatch(context, CreateRule("""{"IpAddresses":["0.0.0.0/0"]}""")));
    }

    /// <summary>
    /// IPv6 网段按前缀判定归属
    /// </summary>
    [Theory]
    [InlineData("2001:db8::1", true)]
    [InlineData("2001:db8:abcd::1", true)]
    [InlineData("2001:db9::1", false)]
    public void IsMatch_Ipv6Cidr_MatchesByPrefix(string clientIp, bool expected)
    {
        var context = new GrayContext { ClientIpAddress = clientIp };

        Assert.Equal(expected, new IpAddressGrayMatcher().IsMatch(context, CreateRule("""{"IpAddresses":["2001:db8::/32"]}""")));
    }

    /// <summary>
    /// IPv6 精确地址匹配
    /// </summary>
    [Fact]
    public void IsMatch_ExactIpv6Entry_MatchesIdenticalAddress()
    {
        var matcher = new IpAddressGrayMatcher();
        var rule = CreateRule("""{"IpAddresses":["::1"]}""");

        Assert.True(matcher.IsMatch(new GrayContext { ClientIpAddress = "::1" }, rule));
        Assert.False(matcher.IsMatch(new GrayContext { ClientIpAddress = "::2" }, rule));
    }

    /// <summary>
    /// IPv4 客户端不会被 IPv6 网段命中
    /// </summary>
    [Fact]
    public void IsMatch_Ipv4ClientAgainstIpv6Network_ReturnsFalse()
    {
        var context = new GrayContext { ClientIpAddress = "192.168.1.1" };

        Assert.False(new IpAddressGrayMatcher().IsMatch(context, CreateRule("""{"IpAddresses":["2001:db8::/32"]}""")));
    }

    /// <summary>
    /// 配置项两侧的空白会被裁掉后再解析
    /// </summary>
    [Theory]
    [InlineData("""{"IpAddresses":[" 192.168.1.10 "]}""")]
    [InlineData("""{"IpAddresses":["\t192.168.1.0/24\n"]}""")]
    public void IsMatch_TrimsWhitespaceAroundEntries(string configuration)
    {
        var context = new GrayContext { ClientIpAddress = "192.168.1.10" };

        Assert.True(new IpAddressGrayMatcher().IsMatch(context, CreateRule(configuration)));
    }

    /// <summary>
    /// 列表里的空白项、非法项被跳过，不影响后面合法项的命中
    /// </summary>
    [Fact]
    public void IsMatch_SkipsBlankAndInvalidEntriesAndKeepsScanning()
    {
        var context = new GrayContext { ClientIpAddress = "192.168.1.10" };
        var rule = CreateRule("""{"IpAddresses":["","   ","not-an-ip","999.1.1.1","192.168.1.10"]}""");

        Assert.True(new IpAddressGrayMatcher().IsMatch(context, rule));
    }

    /// <summary>
    /// 列表里全是空白项或非法项时不命中
    /// </summary>
    [Theory]
    [InlineData("""{"IpAddresses":["","   "]}""")]
    [InlineData("""{"IpAddresses":["not-an-ip"]}""")]
    [InlineData("""{"IpAddresses":["999.1.1.1"]}""")]
    [InlineData("""{"IpAddresses":["192.168.1.0/99"]}""")]
    [InlineData("""{"IpAddresses":["192.168.1.0/"]}""")]
    [InlineData("""{"IpAddresses":["/24"]}""")]
    public void IsMatch_WhenEveryEntryIsUnusable_ReturnsFalse(string configuration)
    {
        var context = new GrayContext { ClientIpAddress = "192.168.1.10" };

        Assert.False(new IpAddressGrayMatcher().IsMatch(context, CreateRule(configuration)));
    }

    /// <summary>
    /// 多条配置项中任意一条命中即整体命中
    /// </summary>
    [Fact]
    public void IsMatch_WithMultipleEntries_MatchesOnAnyHit()
    {
        var matcher = new IpAddressGrayMatcher();
        var rule = CreateRule("""{"IpAddresses":["10.0.0.0/8","172.16.0.0/12","203.0.113.7"]}""");

        Assert.True(matcher.IsMatch(new GrayContext { ClientIpAddress = "10.1.1.1" }, rule));
        Assert.True(matcher.IsMatch(new GrayContext { ClientIpAddress = "172.16.5.5" }, rule));
        Assert.True(matcher.IsMatch(new GrayContext { ClientIpAddress = "203.0.113.7" }, rule));
        Assert.False(matcher.IsMatch(new GrayContext { ClientIpAddress = "8.8.8.8" }, rule));
    }

    /// <summary>
    /// 客户端 IP 缺失或无法解析时不命中
    /// </summary>
    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("192.168.1.256")]
    [InlineData("192.168.1.10/24")]
    public void IsMatch_WhenClientIpIsUnparsable_ReturnsFalse(string clientIp)
    {
        var context = new GrayContext { ClientIpAddress = clientIp };

        Assert.False(new IpAddressGrayMatcher().IsMatch(context, CreateRule("""{"IpAddresses":["192.168.1.0/24"]}""")));
    }

    /// <summary>
    /// 客户端 IP 为 null 时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenClientIpIsNull_ReturnsFalse()
    {
        Assert.False(new IpAddressGrayMatcher().IsMatch(new GrayContext(), CreateRule("""{"IpAddresses":["0.0.0.0/0"]}""")));
    }

    /// <summary>
    /// 客户端 IP 为空串时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenClientIpIsEmpty_ReturnsFalse()
    {
        var context = new GrayContext { ClientIpAddress = string.Empty };

        Assert.False(new IpAddressGrayMatcher().IsMatch(context, CreateRule("""{"IpAddresses":["0.0.0.0/0"]}""")));
    }

    /// <summary>
    /// IP 列表为空数组、显式 null 或字段缺失时不命中
    /// </summary>
    [Theory]
    [InlineData("""{"IpAddresses":[]}""")]
    [InlineData("""{"IpAddresses":null}""")]
    [InlineData("{}")]
    [InlineData("null")]
    public void IsMatch_WhenIpListIsAbsentOrEmpty_ReturnsFalse(string configuration)
    {
        var context = new GrayContext { ClientIpAddress = "192.168.1.10" };

        Assert.False(new IpAddressGrayMatcher().IsMatch(context, CreateRule(configuration)));
    }

    /// <summary>
    /// 配置不是合法 JSON 时吞异常并返回不命中
    /// </summary>
    [Theory]
    [InlineData("not-json")]
    [InlineData("{")]
    [InlineData("""{"IpAddresses":"192.168.1.10"}""")]
    public void IsMatch_WhenConfigurationIsMalformed_ReturnsFalse(string configuration)
    {
        var context = new GrayContext { ClientIpAddress = "192.168.1.10" };

        Assert.False(new IpAddressGrayMatcher().IsMatch(context, CreateRule(configuration)));
    }

    /// <summary>
    /// 配置为 null 时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenConfigurationIsNull_ReturnsFalse()
    {
        var context = new GrayContext { ClientIpAddress = "192.168.1.10" };

        Assert.False(new IpAddressGrayMatcher().IsMatch(context, CreateRule(null)));
    }

    /// <summary>
    /// 配置为空串时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenConfigurationIsEmpty_ReturnsFalse()
    {
        var context = new GrayContext { ClientIpAddress = "192.168.1.10" };

        Assert.False(new IpAddressGrayMatcher().IsMatch(context, CreateRule(string.Empty)));
    }

    /// <summary>
    /// 规则不是 GrayRule 实现时不命中
    /// </summary>
    [Fact]
    public void IsMatch_WhenRuleIsNotGrayRule_ReturnsFalse()
    {
        var context = new GrayContext { ClientIpAddress = "192.168.1.10" };
        var rule = new FakeGrayRule { RuleType = GrayRuleType.IpAddress };

        Assert.False(new IpAddressGrayMatcher().IsMatch(context, rule));
    }

    /// <summary>
    /// 异步重载与同步重载结论一致
    /// </summary>
    [Fact]
    public async Task IsMatchAsync_MirrorsSyncOverload()
    {
        var matcher = new IpAddressGrayMatcher();
        var rule = CreateRule("""{"IpAddresses":["192.168.1.0/24"]}""");
        var token = TestContext.Current.CancellationToken;

        Assert.True(await matcher.IsMatchAsync(new GrayContext { ClientIpAddress = "192.168.1.10" }, rule, token));
        Assert.False(await matcher.IsMatchAsync(new GrayContext { ClientIpAddress = "192.168.2.10" }, rule, token));
        Assert.False(await matcher.IsMatchAsync(new GrayContext(), rule, token));
    }

    /// <summary>
    /// 构造一条 IP 灰度规则
    /// </summary>
    private static GrayRule CreateRule(string? configuration)
    {
        return new GrayRule
        {
            RuleId = "ip-1",
            RuleName = "IP 灰度",
            RuleType = GrayRuleType.IpAddress,
            IsEnabled = true,
            Priority = 1,
            Configuration = configuration
        };
    }
}
