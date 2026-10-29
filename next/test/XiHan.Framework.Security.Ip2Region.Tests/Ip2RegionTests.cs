// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Security.Abstractions.Network;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.Ip2Region.Tests;

/// <summary>
/// ip2region 归属地。
/// </summary>
/// <remarks>
/// 记录文本两种格式的解析、回环与私有网段不查、映射地址按 IPv4 查、库不可用或查询失败时无值、选项与特性装配；设了 <c>XIHAN_TEST_IP2REGION_V4</c>（xdb 文件路径）时再用真实库查一次。
/// </remarks>
public sealed class Ip2RegionTests
{
    /// <summary>
    /// 真实 IPv4 库路径的环境变量。
    /// </summary>
    private const string V4DatabaseVariable = "XIHAN_TEST_IP2REGION_V4";

    [Theory]
    [InlineData("中国|广东省|深圳市|电信|CN", "中国", "广东省", "深圳市", "电信", "CN", "中国 广东省 深圳市 电信")]
    [InlineData("中国|0|广东省|深圳市|电信", "中国", "广东省", "深圳市", "电信", null, "中国 广东省 深圳市 电信")]
    [InlineData("美国|0|0|0|US", "美国", null, null, null, "US", "美国")]
    [InlineData("中国|0|上海|上海市|联通", "中国", "上海", "上海市", "联通", null, "中国 上海 上海市 联通")]
    [InlineData("中国|北京|北京|0|CN", "中国", "北京", "北京", null, "CN", "中国 北京")]
    public void Records_InBothFormats_AreParsed(string record, string? country, string? province, string? city, string? isp, string? code, string location)
    {
        var region = Ip2RegionRecord.Parse(record);

        Assert.Equal(new IpRegion(country, province, city, isp, code), region);
        Assert.Equal(location, region!.Location);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Reserved|Reserved|Reserved|0|0")]
    [InlineData("0|0|0|0|0")]
    public void ReservedOrUnknownRecords_HaveNoRegion(string? record) => Assert.Null(Ip2RegionRecord.Parse(record));

    [Fact]
    public void LoopbackAndPrivateAddresses_AreNotLookedUp_MappedAddressesUseIpv4()
    {
        var asked = new List<string>();
        using var resolver = new Ip2RegionResolver(
            ip =>
            {
                asked.Add(ip);
                return "中国|广东省|深圳市|电信|CN";
            },
            ip =>
            {
                asked.Add(ip);
                return "美国|0|0|0|US";
            },
            NullLogger.Instance);

        foreach (var local in new[] { "127.0.0.1", "::1", "10.1.2.3", "172.16.0.1", "172.31.255.255", "192.168.1.1", "fe80::1", "fec0::1", "ff02::1", "::ffff:192.168.1.1" })
        {
            Assert.False(resolver.Resolve(IPAddress.Parse(local)).HasValue, local);
        }

        Assert.Empty(asked);
        Assert.Equal("深圳市", resolver.Resolve(IPAddress.Parse("::ffff:203.0.113.9")).ValueOrThrow().City);
        Assert.Equal("US", resolver.Resolve(IPAddress.Parse("2001:db8::1")).ValueOrThrow().CountryCode);
        Assert.True(resolver.Resolve(IPAddress.Parse("172.32.0.1")).HasValue);
        Assert.Equal(["203.0.113.9", "2001:db8::1", "172.32.0.1"], asked);
    }

    [Fact]
    public void UnavailableDatabaseOrFailingLookup_HasNoRegion()
    {
        using var resolver = new Ip2RegionResolver(null, static _ => throw new InvalidOperationException("corrupt"), NullLogger.Instance);

        Assert.False(resolver.Resolve(IPAddress.Parse("203.0.113.9")).HasValue);
        Assert.False(resolver.Resolve(IPAddress.Parse("2001:db8::1")).HasValue);
    }

    [Fact]
    public void Options_ReadPaths_AndResolveRelativeOnesAgainstTheBaseDirectory()
    {
        var defaults = Ip2RegionOptions.FromConfiguration(new ConfigurationBuilder().Build());
        Assert.Equal(Path.Combine("IpDatabases", "ip2region_v4.xdb"), defaults.V4Database);
        Assert.Equal(Path.Combine("IpDatabases", "ip2region_v6.xdb"), defaults.V6Database);

        var configured = Ip2RegionOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection([new("XiHan:Security:Ip2Region:V4Database", "data/v4.xdb")]).Build());
        Assert.Equal("data/v4.xdb", configured.V4Database);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "data/v4.xdb"), Ip2RegionOptions.Resolve(configured.V4Database));
        var rooted = Path.Combine(Path.GetTempPath(), "v4.xdb");
        Assert.Equal(rooted, Ip2RegionOptions.Resolve(rooted));
        Assert.Throws<ArgumentException>(static () => new Ip2RegionOptions { V6Database = " " });
    }

    [Fact]
    public async Task Feature_RegistersTheResolver_AndMissingDatabasesYieldNoRegion()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<Ip2RegionSecurityFeature>(),
            [new("XiHan:Security:Ip2Region:V4Database", Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.xdb"))]);

        var resolver = host.Services.GetRequiredService<IIpRegionResolver>();
        Assert.IsType<Ip2RegionResolver>(resolver);
        Assert.False(resolver.Resolve(IPAddress.Parse("203.0.113.9")).HasValue);
    }

    [Fact]
    public void RealDatabase_ResolvesAPublicAddress()
    {
        var path = Environment.GetEnvironmentVariable(V4DatabaseVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(path), $"Set the environment variable '{V4DatabaseVariable}' to an ip2region IPv4 xdb file to run this test.");

        using var resolver = new Ip2RegionResolver(new Ip2RegionOptions { V4Database = path! }, NullLoggerFactory.Instance);

        // 114.114.114.114 是国内公共 DNS
        Assert.Equal("中国", resolver.Resolve(IPAddress.Parse("114.114.114.114")).ValueOrThrow().Country);
        Assert.False(resolver.Resolve(IPAddress.Parse("192.168.0.1")).HasValue);
    }
}
