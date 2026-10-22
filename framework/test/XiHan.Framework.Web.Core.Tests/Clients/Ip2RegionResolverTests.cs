// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using XiHan.Framework.Web.Core.Clients;
using XiHan.Framework.Web.Core.Options;
using XiHan.Framework.Web.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Web.Core.Tests.Clients;

/// <summary>
/// IP 归属地解析
/// </summary>
/// <remarks>
/// 记录格式有新旧两版，<c>0</c> 表示未知，港澳台的国家是「中国」——这些都写死在解析里，访问策略的地区条件按它取值。
/// 解析器在库文件缺失、未启用、回环与内网地址时都给空，不能把请求打挂；仓库里不放 xdb，真实查库由应用侧验证。
/// </remarks>
public class Ip2RegionResolverTests
{
    /// <summary>
    /// 新版记录：国家|省份|城市|运营商|国家代码
    /// </summary>
    [Fact]
    public void TryParse_NewFormat()
    {
        Assert.True(IpRegion.TryParse("中国|福建省|福州市|中国电信|CN", out var region));
        Assert.Equal(new IpRegion("中国", "福建省", "福州市", "中国电信", "CN"), region);
        Assert.Equal("中国 福建省 福州市 中国电信 CN", region.Location);
    }

    /// <summary>
    /// 港澳台：国家是中国，省份是特别行政区或台湾省
    /// </summary>
    [Fact]
    public void TryParse_HongKongMacauTaiwan_AreChina()
    {
        Assert.True(IpRegion.TryParse("中国|台湾省|高雄市|远传电信|CN", out var region));
        Assert.Equal("中国", region.Country);
        Assert.Equal("台湾省", region.Province);
    }

    /// <summary>
    /// 未知段（0）记为空，展示文本跳过它们
    /// </summary>
    [Fact]
    public void TryParse_UnknownParts_AreNull()
    {
        Assert.True(IpRegion.TryParse("Australia|Queensland|0|0|AU", out var region));
        Assert.Null(region.City);
        Assert.Null(region.Isp);
        Assert.Equal("Australia Queensland AU", region.Location);
    }

    /// <summary>
    /// 旧版记录：国家|区域|省份|城市|运营商，没有国家代码
    /// </summary>
    [Fact]
    public void TryParse_LegacyFormat()
    {
        Assert.True(IpRegion.TryParse("中国|0|广东省|深圳市|电信", out var region));
        Assert.Equal(new IpRegion("中国", "广东省", "深圳市", "电信", null), region);
    }

    /// <summary>
    /// 保留地址、全部未知、空文本都解析不出
    /// </summary>
    [Theory]
    [InlineData("Reserved|Reserved|Reserved|0|0")]
    [InlineData("0|0|0|0|0")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_NoInformation_ReturnsFalse(string? record)
    {
        Assert.False(IpRegion.TryParse(record, out var region));
        Assert.Null(region);
    }

    /// <summary>
    /// 回环、内网（含 IPv4 映射）不查库，直接为空
    /// </summary>
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.20.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("::ffff:192.168.1.1")]
    [InlineData("fe80::1")]
    public void Resolve_LoopbackAndPrivate_ReturnsNull(string address)
    {
        Assert.Null(CreateResolver().Resolve(IPAddress.Parse(address)));
    }

    /// <summary>
    /// 库文件缺失、未启用：公网地址也为空，不抛异常
    /// </summary>
    [Fact]
    public void Resolve_MissingDatabaseOrDisabled_ReturnsNull()
    {
        Assert.Null(CreateResolver().Resolve(IPAddress.Parse("203.0.113.9")));
        Assert.Null(CreateResolver().Resolve(IPAddress.Parse("2001:db8::1")));
        Assert.Null(CreateResolver(new XiHanClientInfoOptions { EnableIpRegion = false }).Resolve(IPAddress.Parse("203.0.113.9")));
    }

    /// <summary>
    /// 从未加载过库时释放不报错，且允许重复释放
    /// </summary>
    [Fact]
    public void Dispose_WithoutSearcher_IsSafeAndIdempotent()
    {
        var resolver = CreateResolver();

        var exception = Record.Exception(() =>
        {
            resolver.Dispose();
            resolver.Dispose();
        });

        Assert.Null(exception);
    }

    private static Ip2RegionResolver CreateResolver(XiHanClientInfoOptions? options = null)
    {
        var missing = Path.Combine(Path.GetTempPath(), "xihan-no-ipdb-" + Guid.NewGuid().ToString("N"));
        var hostingEnvironment = new EmptyHostingEnvironment
        {
            EnvironmentName = "Testing",
            ApplicationName = "XiHan.Framework.Web.Core.Tests",
            ContentRootPath = missing,
            WebRootPath = missing
        };

        return new Ip2RegionResolver(
            hostingEnvironment,
            new OptionsWrapper<XiHanClientInfoOptions>(options ?? new XiHanClientInfoOptions()),
            NullLogger<Ip2RegionResolver>.Instance);
    }
}
